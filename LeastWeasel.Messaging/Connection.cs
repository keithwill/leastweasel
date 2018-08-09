using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

namespace LeastWeasel.Messaging 
{

    public class Connection : IDisposable
    {
    
        private ConcurrentDictionary<long, ResponseCorrelation> messageCorrelationLookup = new ConcurrentDictionary<long, ResponseCorrelation>();
        private AsyncQueue<(string method, object message, long messageId)> sendQueue = new AsyncQueue<(string, object, long)>();
        private readonly Client client;
        private long nextMessageId;
        private Socket socket;

        public Connection(Client client)
        {
            this.client = client;
            this.nextMessageId = 0;
            this.socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        } 

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            await this.socket.ConnectAsync(client.HostName, client.Port);
            _ = Task.Run( async () => { await ReceiveLoop(cancellationToken);});
            _ = Task.Run( async () => { await SendLoop(cancellationToken);});
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            var pipe = new Pipe();
            Task writing = FillPipeAsync(socket, pipe.Writer);
            Task reading = ReadPipeAsync(socket, pipe.Reader);
            await Task.WhenAll(reading, writing);
        }

    private async Task ReadPipeAsync(Socket socket, PipeReader reader)
    {

            const int messageHeaderLength = 8 + 4 + 8; // messageId, methodHash, messageLength

            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;
                bool readMessage;

                do
                {

                    readMessage = false;

                    if (buffer.Length > messageHeaderLength)
                    {
                        //We have enough to get the method name and length of message
                        var messageLengthBytes = buffer.Slice(16, 4).ToArray();
                        var messageLength = BitConverter.ToInt32(messageLengthBytes);

                        var endOfMessage = messageLength + messageHeaderLength;

                        if (buffer.Length >= endOfMessage)
                        {
                            // We have at least one message in the buffer

                            var messageIdBytes = buffer.Slice(0, 8).ToArray();
                            var methodHashBytes = buffer.Slice(8, 8).ToArray();

                            var messageId = BitConverter.ToInt64(messageIdBytes);
                            var methodHash = BitConverter.ToInt64(methodHashBytes);
                            var method = client.Service.HashMethodLookup[methodHash];
                            var message = client.Service.ResponseDeserializers[method](buffer.Slice(messageHeaderLength, messageLength).ToArray());
                            buffer = buffer.Slice(endOfMessage);
                            readMessage = true;
                            
                            if (messageCorrelationLookup.TryRemove(messageId, out var correlation))
                            {
                                correlation.TaskCompletionSource.SetResult(message);
                            }
                        }  

                    }

                }
                while (readMessage);

                // We sliced the buffer until no more data could be processed
                // Tell the PipeReader how much we consumed and how much we left to process
                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            reader.Complete();
    }


        private async Task FillPipeAsync(Socket socket, PipeWriter writer)
        {
                const int minimumBufferSize = 512;

                while (true)
                {
                    try
                    {
                        // Request a minimum of 512 bytes from the PipeWriter
                        Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                        int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        // Tell the PipeWriter how much was read
                        writer.Advance(bytesRead);
                    }
                    catch
                    {
                        break;
                    }

                    // Make the data available to the PipeReader
                    FlushResult result = await writer.FlushAsync();

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                // Signal to the reader that we're done writing
                writer.Complete();
        }

        private async Task SendLoop(CancellationToken cancellationToken)
        {
            byte[] sendBuffer = new byte[4096];
            const int messageOverhead = 8 + 4 + 8; // messageId, messageLength, methodHash

            do
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var messageCall = await sendQueue.DequeueAsync(cancellationToken);
                    var messageIdBytes = BitConverter.GetBytes(messageCall.messageId);
                    var methodHashBytes = BitConverter.GetBytes(client.Service.MethodHashLookup[messageCall.method]);
                    var serializedMessageLength = LZ4MessagePackSerializer.SerializeToBlock(ref sendBuffer, messageOverhead, messageCall.message, MessagePackSerializer.DefaultResolver );
                    var messageLengthBytes = BitConverter.GetBytes(serializedMessageLength);
                    Buffer.BlockCopy(messageIdBytes, 0, sendBuffer, 0, messageIdBytes.Length);
                    Buffer.BlockCopy(methodHashBytes, 0, sendBuffer, 8, methodHashBytes.Length);
                    Buffer.BlockCopy(messageLengthBytes, 0, sendBuffer, 16, messageLengthBytes.Length);
                    //Console.WriteLine($"Socket:{socket.Handle} >>> Id:{messageCall.messageId} Method:{messageCall.method} Bytes:{serializedMessageLength}");
                    await socket.SendAsync(sendBuffer.AsMemory(0, messageOverhead + serializedMessageLength), SocketFlags.None, cancellationToken);
                }
            } while (!cancellationToken.IsCancellationRequested);
            this.socket.Close();
        }

        public void Send(string method, object message)
        {
            var messageId = Interlocked.Increment(ref this.nextMessageId);
            Send(method, message, messageId);
        }

        private void Send(string method, object message, long messageId)
        {
            sendQueue.Enqueue( (method, message, messageId) );
        }

        public Task<object> Request<TRequest>(string method, TRequest request)
        {
            var messageId = Interlocked.Increment(ref this.nextMessageId);

            var responseCorrelation = new ResponseCorrelation(
                messageId,
                method,
                new TaskCompletionSource<object>()
            );
            messageCorrelationLookup.TryAdd(messageId, responseCorrelation);

            Send(method, request, messageId);

            return responseCorrelation.TaskCompletionSource.Task;
        }

        public void Dispose()
        {
            this.socket.Close(1);
            this.socket?.Dispose();
        }



        
    }
}