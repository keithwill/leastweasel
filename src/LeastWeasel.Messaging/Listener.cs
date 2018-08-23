using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using MessagePack;

namespace LeastWeasel.Messaging
{
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

            listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            listenSocket.Listen(100);
            while (true)
            {
                var client = await listenSocket.AcceptAsync();

                var returnQueue = new AsyncQueue<(long messageId, long methodHash, object message)>();
                _ = ProcessReturnMessages(client, returnQueue, cancellationToken);
                _ = ProcessMessagesAsync(client, returnQueue, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private async Task ProcessReturnMessages(
            Socket client,
            AsyncQueue<(long messageId, long methodHash, object message)> returnQueue,
            CancellationToken cancellationToken
        )
        {

            const int messageOverhead = 8 + 8 + 4; // messageId, methodHash, messageLength
            using (var mem = new MemoryStream(512))
            using (var bin = new BinaryWriter(mem))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var returnCall = await returnQueue.DequeueAsync(cancellationToken);
                    // var messageIdBytes = returnCall.messageIdBytes;
                    // var methodHashBytes = returnCall.methodHashBytes;
                    mem.Seek(messageOverhead, SeekOrigin.Begin);

                    //MessagePackSerializer.Serialize(mem, returnCall.message);
                    MessagePackSerializer.Serialize(mem, returnCall.message);

                    var messageLength = (int)(mem.Position - messageOverhead);
                    //var messageLength = LZ4MessagePackSerializer.SerializeToBlock(ref sendBuffer, messageOverhead, returnCall.message, MessagePackSerializer.DefaultResolver);

                    mem.Seek(0, SeekOrigin.Begin);
                    var messageLengthBytes = BitConverter.GetBytes(messageLength);
                    bin.Write(returnCall.messageId);
                    bin.Write(returnCall.methodHash);
                    bin.Write(messageLength);

                    var sendBuffer = mem.GetBuffer();
                    var sendSegment = new ArraySegment<byte>(sendBuffer, 0, messageOverhead + (int)messageLength);

                    await client.SendAsync(sendSegment, SocketFlags.None);
                }
            }
        }

        private async Task ProcessMessagesAsync(
            Socket client,
            AsyncQueue<(long messageId, long methodHash, object message)> returnQueue,
            CancellationToken cancellationToken
        )
        {
            var pipeOptions = new PipeOptions(null, null, null, 4_000_000, 3_000_000);
            var pipe = new Pipe(pipeOptions);
            Task writing = FillPipeAsync(client, pipe.Writer, cancellationToken);
            Task reading = ReadPipeAsync(client, pipe.Reader, returnQueue, cancellationToken);
            await Task.WhenAll(reading, writing);
        }

        private async Task ReadPipeAsync(
            Socket socket,
            PipeReader reader,
            AsyncQueue<(long messageId, long methodHash, object message)> returnQueue,
            CancellationToken cancellationToken
        )
        {

            const int messageHeaderLength = 8 + 4 + 8; // messageId, methodHash, messageLength
            var deserializeBuffer = new byte[512];

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
                        var messageLengthBytes = buffer.Slice(16, 4).ToArray();
                        var messageLength = BitConverter.ToInt32(messageLengthBytes);
                        var endOfMessage = messageLength + messageHeaderLength;

                        if (buffer.Length >= endOfMessage)
                        {
                            var messageIdBytes = buffer.Slice(0, 8).ToArray();
                            var methodHashBytes = buffer.Slice(8, 8).ToArray();
                            var messageId = BitConverter.ToInt64(messageIdBytes);
                            var methodHash = BitConverter.ToInt64(methodHashBytes);
                            var method = service.HashMethodLookup[methodHash];
                            var deserializer = service.RequestDeserializers[method];
                            if (deserializeBuffer.Length < messageLength)
                            {
                                Array.Resize(ref deserializeBuffer, messageLength);
                            }
                            buffer.Slice(messageHeaderLength, messageLength).CopyTo(deserializeBuffer);
                            var deserializeSegment = new ArraySegment<byte>(deserializeBuffer, 0, messageLength);
                            var message = deserializer(deserializeSegment);
                            buffer = buffer.Slice(endOfMessage);

                            readMessage = true;
                            if (service.SendHandlers.TryGetValue(method, out var handler))
                            {
                                _ = Task.Run(async () => await handler(message));
                            }
                            else if (service.RequestHandlers.TryGetValue(method, out var requestHandler))
                            {
                                _ = Task.Run(async () =>
                                {
                                    var returnMessage = await requestHandler(message);
                                    returnQueue.Enqueue((messageId, methodHash, returnMessage));
                                });
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

        private async Task FillPipeAsync(Socket socket, PipeWriter writer, CancellationToken cancellationToken)
        {
            const int minimumBufferSize = 512;

            while (true)
            {
                try
                {
                    // Request a minimum of 512 bytes from the PipeWriter
                    Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, cancellationToken);
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
                FlushResult result = await writer.FlushAsync(cancellationToken);

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

}