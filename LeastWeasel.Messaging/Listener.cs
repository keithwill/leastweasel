
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

public class Listener
{
    private const int DEFAULT_PORT = 8888;
    private readonly Service service;
    private readonly int port;
    private Socket listenSocket;

    private AsyncQueue<(Socket socket, byte[] messageIdBytes, byte[] methodHashBytes, object message)> returnMessageQueue;

    public Listener(Service service, int port = DEFAULT_PORT)
    {
        this.service = service;
        this.port = port;
        this.listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        this.returnMessageQueue = new AsyncQueue<(Socket, byte[], byte[], object)>();
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _ = ProcessReturnMessages(cancellationToken);
        listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        listenSocket.Listen(100);
        while(true)
        {
            var client = await listenSocket.AcceptAsync();
            //Console.WriteLine($"Listener Connected Client:{client.Handle}");
            _ = ProcessMessagesAsync(client);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private async Task ProcessReturnMessages(CancellationToken cancellationToken)
    {
        var sendBuffer = new byte[512];
        const int messageOverhead = 8 + 8 + 4; // messageId, methodHash, messageLength

        while (!cancellationToken.IsCancellationRequested)
        {
            var returnCall = await returnMessageQueue.DequeueAsync(cancellationToken);
            var messageIdBytes = returnCall.messageIdBytes;
            var methodHashBytes = returnCall.methodHashBytes;
            var messageLength = LZ4MessagePackSerializer.SerializeToBlock(ref sendBuffer, messageOverhead, returnCall.message, MessagePackSerializer.DefaultResolver );
            var messageLengthBytes = BitConverter.GetBytes(messageLength);
            Buffer.BlockCopy(messageIdBytes, 0, sendBuffer, 0, messageIdBytes.Length);
            Buffer.BlockCopy(methodHashBytes, 0, sendBuffer, 8, methodHashBytes.Length);
            Buffer.BlockCopy(messageLengthBytes, 0, sendBuffer, 16, messageLengthBytes.Length);
            await returnCall.socket.SendAsync(sendBuffer.AsMemory(0, messageLength + messageOverhead), SocketFlags.None);
        }
    }

    private async Task ProcessMessagesAsync(Socket client)
    {
        var pipe = new Pipe();
        Task writing = FillPipeAsync(client, pipe.Writer);
        Task reading = ReadPipeAsync(client, pipe.Reader);
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
                    //Console.WriteLine($"buffer.length: {buffer.Length} > messageHeaderLength:{messageHeaderLength}");
                    if (buffer.Length > messageHeaderLength)
                    {
                        //We have enough to get the method name and length of message
                        var messageLengthBytes = buffer.Slice(16, 4).ToArray();
                        var messageLength = BitConverter.ToInt32(messageLengthBytes);

                        var endOfMessage = messageLength + messageHeaderLength;

                        //Console.WriteLine($"buffer.length: {buffer.Length} > endOfMessage:{endOfMessage}");
                        if (buffer.Length >= endOfMessage)
                        {

                            
                            // We have at least one message in the buffer

                            var messageIdBytes = buffer.Slice(0, 8).ToArray();
                            var methodHashBytes = buffer.Slice(8, 8).ToArray();

                            var messageId = BitConverter.ToInt64(messageIdBytes);
                            var methodHash = BitConverter.ToInt64(methodHashBytes);
                            var method = service.HashMethodLookup[methodHash];
                            //Console.WriteLine($"Listener <<< Id:{messageId} from {socket.Handle}");
                            var deserializer = service.RequestDeserializers[method];
                            var messageBytes = buffer.Slice(messageHeaderLength, messageLength).ToArray();
                            //Console.WriteLine($"header{messageHeaderLength} messageLength:{messageLength} messageBytesLength:{messageBytes.Length}");
                            var message = deserializer.Invoke(messageBytes);
                            // Console.WriteLine("Deserialized Object");
                            buffer = buffer.Slice(endOfMessage);
                            readMessage = true;
                            _ = Task.Run(async () => {                                
                                var returnMessage = await service.Handlers[method](message);
                                returnMessageQueue.Enqueue((socket, messageIdBytes, methodHashBytes, returnMessage));
                            });
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
            //Console.WriteLine($"Done reading from Client:{socket.Handle} ");
            // Signal to the reader that we're done writing
            writer.Complete();
    }
}