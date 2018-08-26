using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LeastWeasel.Abstractions;
using LeastWeasel.Messaging.File;
using MessagePack;

namespace LeastWeasel.Messaging
{

    public class Client : IDisposable, IClient
    {

        private ConcurrentDictionary<long, TaskCompletionSource<object>> messageCorrelationLookup = new ConcurrentDictionary<long, TaskCompletionSource<object>>();
        private AsyncQueue<(string method, object message, long messageId)> sendQueue =
            new AsyncQueue<(string, object, long)>();

        private long nextMessageId;
        private Socket socket;
        private readonly Service service;
        private readonly string hostName;
        private readonly int port;
        private ConnectionType connectionType;
        private CancellationToken shutdownToken;
        private CancellationTokenSource shutdownTokenSource;
        private bool disposed;

        public Client(Service service, string hostName, int port)
        {
            if (string.IsNullOrWhiteSpace(hostName))
            {
                throw new ArgumentException($"{nameof(hostName)} was null or empty");
            }
            if (port <= 0)
            {
                throw new ArgumentException($"{nameof(port)} must be greater than zero");
            }
            this.nextMessageId = 0;
            this.socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            this.service = service;
            this.hostName = hostName;
            this.port = port;
            this.connectionType = ConnectionType.Client;
        }

        public Client(Service service, Socket socket)
        {
            this.service = service;
            this.socket = socket;
            this.connectionType = ConnectionType.Listener;
        }

        public async Task ConnectAsync()
        {
            if (this.socket.Connected)
            {
                throw new InvalidOperationException("Connection is already connected");
            }
            this.shutdownTokenSource = new CancellationTokenSource();
            this.shutdownToken = shutdownTokenSource.Token;
            await this.socket.ConnectAsync(this.hostName, this.port);
            _ = Task.Run(async () => { await ReceiveLoop(); });
            _ = Task.Run(async () => { await SendLoop(); });

        }

        public void BeginMessaging()
        {
            if (!this.socket.Connected)
            {
                throw new InvalidOperationException($"BeginMessaging may only be called on an active Connection");
            }
            this.shutdownTokenSource = new CancellationTokenSource();
            this.shutdownToken = shutdownTokenSource.Token;
            _ = Task.Run(async () => { await ReceiveLoop(); });
            _ = Task.Run(async () => { await SendLoop(); });
        }

        private async Task ReceiveLoop()
        {
            var pipeOptions = new PipeOptions(null, null, null, 4_000_000L, 3_000_000);
            var pipe = new Pipe(pipeOptions);
            Task writing = FillPipeAsync(socket, pipe.Writer);
            Task reading = ReadPipeAsync(socket, pipe.Reader);
            await Task.WhenAll(reading, writing);
        }

        private async Task ReadPipeAsync(Socket socket, PipeReader reader)
        {

            const int messageHeaderLength = 8 + 4 + 8; // messageId, methodHash, messageLength
            var deserializeBuffer = new byte[512];

            while (!shutdownToken.IsCancellationRequested)
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
                            var method = this.service.HashMethodLookup[methodHash];
                            if (deserializeBuffer.Length < messageLength)
                            {
                                Array.Resize(ref deserializeBuffer, messageLength);
                            }
                            buffer.Slice(messageHeaderLength, messageLength).CopyTo(deserializeBuffer);
                            var deserializeSegment = new ArraySegment<byte>(deserializeBuffer, 0, messageLength);
                            object message = null;
                            if (connectionType == ConnectionType.Client)
                            {
                                message = service.ResponseDeserializers[method](deserializeSegment);
                            }
                            else if (connectionType == ConnectionType.Listener)
                            {
                                message = service.RequestDeserializers[method](deserializeSegment);
                            }
                            buffer = buffer.Slice(endOfMessage);
                            readMessage = true;

                            // Check if this message matches a sent request for this connection
                            // or if there is a handler registered for the message
                            if (messageCorrelationLookup.TryRemove(messageId, out var correlation))
                            {
                                correlation.SetResult(message);
                            }

                            if (service.RequestHandlers.TryGetValue(method, out var requestHandler))
                            {
                                _ = Task.Run(async () =>
                                {
                                    var returnMessage = await requestHandler(message);
                                    Send(method, returnMessage, messageId);
                                });
                            }

                            if (service.SendHandlers.TryGetValue(method, out var sendHandler))
                            {
                                _ = Task.Run(async () =>
                                {
                                    await sendHandler(message);
                                });
                            }

                        }

                    }

                }
                while (readMessage && !shutdownToken.IsCancellationRequested);

                // We sliced the buffer until no more data could be processed
                // Tell the PipeReader how much we consumed and how much we left to process
                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted || shutdownToken.IsCancellationRequested)
                {
                    break;
                }
            }

            reader.Complete();
        }


        private async Task FillPipeAsync(Socket socket, PipeWriter writer)
        {
            const int minimumBufferSize = 512;

            while (!shutdownToken.IsCancellationRequested)
            {
                try
                {
                    // Request a minimum of 512 bytes from the PipeWriter
                    Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, shutdownToken);
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

        private async Task SendLoop()
        {
            //byte[] sendBuffer = new byte[512];
            const int messageOverhead = 8 + 4 + 8; // messageId, messageLength, methodHash

            using (var mem = new MemoryStream(512))
            using (var bin = new BinaryWriter(mem))
            {
                while (!shutdownToken.IsCancellationRequested)
                {
                    var messageCall = await sendQueue.DequeueAsync(shutdownToken);
                    mem.Seek(messageOverhead, SeekOrigin.Begin);
                    MessagePackSerializer.Serialize(mem, messageCall.message);
                    var messageLength = (int)mem.Position - messageOverhead;
                    mem.Seek(0, SeekOrigin.Begin);
                    bin.Write(messageCall.messageId);
                    bin.Write(service.MethodHashLookup[messageCall.method]);
                    bin.Write(messageLength);
                    int sendLength = messageOverhead + messageLength;
                    var sendBuffer = mem.GetBuffer().AsMemory(0, sendLength);
                    _ = await socket.SendAsync(sendBuffer, SocketFlags.None, shutdownToken);
                }
            }

            this.socket.Close();
        }

        private void Send(string method, object message, long messageId)
        {
            sendQueue.Enqueue((method, message, messageId));
        }

        public void Dispose()
        {
            if (!disposed)
            {
                shutdownTokenSource.Cancel();
                this.socket.Close(1);
                this.socket?.Dispose();
                disposed = true;
            }

        }

        public void Send<TRequest>(string method, TRequest message)
        {
            var messageId = Interlocked.Increment(ref this.nextMessageId);
            Send(method, message, messageId);
        }

        public async Task<TResult> Request<TRequest, TResult>(string method, TRequest request)
        {
            var messageId = Interlocked.Increment(ref this.nextMessageId);
            var completionSource = new TaskCompletionSource<object>(shutdownToken);
            messageCorrelationLookup.TryAdd(messageId, completionSource);
            Send(method, request, messageId);
            return (TResult)(await completionSource.Task);
        }


        private void GetAllFiles(DirectoryInfo directory, List<string> files)
        {
            files.AddRange(directory.GetFiles().Select(x => x.FullName));
            foreach (var subdirectory in directory.GetDirectories())
            {
                GetAllFiles(subdirectory, files);
            }
        }

        public async Task SendDirectory(string directoryPath, string remoteDirectoryPath)
        {
            var directory = new System.IO.DirectoryInfo(directoryPath);
            var directoryFullPath = System.IO.Path.GetFullPath(directory.FullName);
            var parentDirectory = directory.Parent.FullName;
            var files = new List<string>();
            GetAllFiles(directory, files);

            for (int i = 0; i < files.Count; i += 4)
            {
                var filesToSend = Math.Min(4, files.Count - i);
                if (filesToSend > 0)
                {
                    var tasks = new Task[filesToSend];
                    for (int j = 0; j < filesToSend; j++)
                    {
                        var file = files[j];
                        var relativeRemotePath = file.Remove(0, parentDirectory.Length + 1);
                        tasks[j] = SendFile(file, relativeRemotePath);
                    }
                    await Task.WhenAll(tasks);
                }
            }

        }

        public async Task SendFile(string filePath, string remoteFilePath)
        {
            var chunkSize = 1048576;

            using (var fs = new System.IO.FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var fileLength = fs.Length;
                var toRead = fs.Length;
                byte[] buffer = null;
                var offset = 0L;

                while (toRead > 0)
                {

                    if (buffer == null)
                    {
                        buffer = new byte[Math.Min(chunkSize, toRead)];
                    }

                    var read = await fs.ReadAsync(buffer, 0, buffer.Length);

                    var fileSendResponse = await Request<FileChunkSend, FileChunkSendResponse>("SendFileChunk", new FileChunkSend
                    {
                        FilePath = remoteFilePath,
                        Offset = offset,
                        Data = buffer
                    });

                    offset += read;
                    toRead = fileLength - offset;
                }

            }
        }



    }
}