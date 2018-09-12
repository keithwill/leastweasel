using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using LeastWeasel.Abstractions;
using LeastWeasel.Messaging.File;

namespace LeastWeasel.Messaging
{

    public class Client : IDisposable, IClient
    {

        private ConcurrentDictionary<long, TaskCompletionSource<object>> messageCorrelationLookup = new ConcurrentDictionary<long, TaskCompletionSource<object>>();
        private AsyncQueue < (string method, object message, MessageType messageType, long messageId, Guid reliableId) > sendQueue =
            new AsyncQueue < (string, object, MessageType, long, Guid reliableId) > ();

        private long nextMessageId;
        private Socket socket;
        private readonly Service service;
        private readonly string hostName;
        private readonly int port;
        private CancellationToken shutdownToken;
        private CancellationTokenSource shutdownTokenSource;
        private bool disposed;
        private object initializeReliableDatabaseLock = new object();

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
        }

        public Client(Service service, Socket socket)
        {
            this.service = service;
            this.socket = socket;
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
            _ = Task.Run(() => { ReceiveMessages(); });
            _ = Task.Run(() => { SendLoop(); });

        }

        public void BeginMessaging()
        {
            if (!this.socket.Connected)
            {
                throw new InvalidOperationException($"BeginMessaging may only be called on an active Connection");
            }
            this.shutdownTokenSource = new CancellationTokenSource();
            this.shutdownToken = shutdownTokenSource.Token;
            _ = Task.Run(() => { ReceiveMessages(); });
            _ = Task.Run(() => { SendLoop(); });
        }

        private void ReceiveMessages()
        {

            var readBuffer = new byte[service.MaxMessageSize + 48];
            //Span<byte> readBufferSpan = readBuffer.AsSpan(0);
            int readInMessage = 0;

            var messageBodyLength = 0;
            var messageHeaderLength = 0;
            var messageLength = 0;
            var messageId = 0L;
            var reliableMessageId = Guid.Empty;
            int offset = 0;

            MessageType messageType = MessageType.Send;

            while (!shutdownToken.IsCancellationRequested)
            {

                var read = socket.Receive(readBuffer.AsSpan(readInMessage));
                //Console.WriteLine($"Receiving {read}");
                //readBufferSpan = readBufferSpan.Slice(read);

                if (read == 0)
                {
                    socket.Close();
                    break;
                }

                readInMessage += read;

                bool readMessage;

                do
                {

                    readMessage = false;

                    if (readInMessage - offset < 5)
                    {
                        continue;
                    }

                    if (messageLength == 0)
                    {
                        var messagePreambleBytes = readBuffer.AsSpan(offset, 5);

                        messageBodyLength = MemoryMarshal.Cast<byte, int>(messagePreambleBytes) [0];
                        messageType = (MessageType) messagePreambleBytes[4];
                        switch (messageType)
                        {
                            case MessageType.Send:
                                messageHeaderLength = Constants.SEND_HEADER_SIZE;
                                break;
                            case MessageType.Request:
                            case MessageType.Response:
                            case MessageType.ReliableRequest:
                                messageHeaderLength = Constants.REQUEST_HEADER_SIZE;
                                break;
                        }
                        messageLength = messageHeaderLength + messageBodyLength;

                        // Compact the read buffer back to the start and reset the offset
                        // Because otherwise we would run out of buffer space
                        if (offset + messageLength > readBuffer.Length)
                        {
                            Console.WriteLine("Compacting read buffer");
                            for (int i = messageLength; i < readInMessage; i++)
                            {
                                readBuffer[i - messageLength] = readBuffer[i];
                            }
                            offset = 0;
                        }
                    }

                    if (readInMessage - offset >= messageLength)
                    {
                        //Console.WriteLine("Have enough for a message");
                        // We have at least one message in the buffer
                        var methodHashBytes = readBuffer.AsSpan(offset + 5, 8);
                        var methodHash = MemoryMarshal.Cast<byte, long>(methodHashBytes) [0];
                        var method = this.service.HashMethodLookup[methodHash];

                        if (messageType == MessageType.Request ||
                            messageType == MessageType.Response ||
                            messageType == MessageType.ReliableRequest)
                        {
                            var messageIdBytes = readBuffer.AsSpan(offset + 13, 8);
                            messageId = MemoryMarshal.Cast<byte, long>(messageIdBytes) [0];
                        }

                        var messageBuffer = readBuffer.AsSpan(offset + messageHeaderLength, messageBodyLength);
                        //var deserializeSegment = new ArraySegment<byte>(deserializeBuffer, 0, messageBodyLength);
                        object message = null;

                        switch (messageType)
                        {
                            case MessageType.Send:
                            case MessageType.Request:
                            case MessageType.ReliableRequest:
                                message = service.RequestDeserializers[method](ref messageBuffer);
                                break;
                            case MessageType.Response:
                                message = service.ResponseDeserializers[method](ref messageBuffer);
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        switch (messageType)
                        {
                            case MessageType.Send:
                                if (service.SendHandlers.TryGetValue(method, out var sendHandler))
                                {
                                    var messageToSend = message;
                                    _ = Task.Run(async() =>
                                    {
                                        await sendHandler(messageToSend);
                                    });
                                }
                                break;
                            case MessageType.Request:
                            case MessageType.ReliableRequest:
                                if (service.RequestHandlers.TryGetValue(method, out var requestHandler))
                                {
                                    var methodToSend = method;
                                    var idToSend = messageId;
                                    var messageToHandle = message;
                                    //Console.WriteLine($"{DateTime.Now} - Starting task for request handler " + method);
                                    _ = Task.Run(async() =>
                                    {
                                        //Console.WriteLine($"{DateTime.Now} - Starting request handler for " + method);
                                        var returnMessage = await requestHandler(messageToHandle);
                                        //Console.WriteLine($"{DateTime.Now} - Finished request handler for " + method);
                                        //Console.WriteLine("Queueing Return Message for " + method);
                                        Send(methodToSend, returnMessage, MessageType.Response, idToSend, Guid.Empty);
                                    });
                                }
                                break;
                            case MessageType.Response:
                                if (messageCorrelationLookup.TryRemove(messageId, out var correlation))
                                {
                                    correlation.SetResult(message);
                                    //Console.WriteLine($"Result Set:{messageId}");
                                }
                                else
                                {
                                    Console.WriteLine($"Failed to get correlation for {messageId}");
                                }
                                break;

                        }

                    }

                    var remaining = readInMessage - (offset + messageLength);
                    if (remaining > 0)
                    {
                        //Console.WriteLine($"Read more than one message worth, keeping offset for now");
                        offset += messageLength;
                        readMessage = true;
                    }

                    if (remaining == 0)
                    {
                        readInMessage = 0; // We have read everything exactly, start buffer over
                        offset = 0;
                    }

                    messageLength = 0;

                }
                while (readMessage && !shutdownToken.IsCancellationRequested);

            }
        }

        private void SendLoop()
        {

            var buffer = new byte[service.MaxMessageSize];

            while (!shutdownToken.IsCancellationRequested)
            {
                var messageCall = sendQueue.DequeueAsync(shutdownToken).Result;
                //Console.WriteLine($"Dequeued: {messageCall.messageId}");
                //Console.WriteLine("Messages to SEND");
                if (messageCall.messageType == MessageType.ReliableRequest)
                {
                    var db = service.GetReliableRequestDatabase(messageCall.method);
                    var requestsCollection = db.GetCollection<ReliableMessage>("Requests");

                    var reliableMessage = new ReliableMessage
                    {
                        MessageId = messageCall.reliableId,
                        Method = messageCall.method,
                        Request = messageCall.message
                    };
                    requestsCollection.Insert(reliableMessage);
                }

                var position = 4; // leave room for length
                buffer[position] = (byte) messageCall.messageType;
                position += 1;
                var methodHash = service.MethodHashLookup[messageCall.method];
                MemoryMarshal.Cast<byte, long>(buffer.AsSpan(position, 8)) [0] = methodHash;
                position += 8;

                switch (messageCall.messageType)
                {
                    case MessageType.Send:
                        // Do nothing for now
                        break;
                    case MessageType.Request:
                    case MessageType.Response:
                    case MessageType.ReliableRequest:
                        MemoryMarshal.Cast<byte, long>(buffer.AsSpan(position, 8)) [0] = messageCall.messageId;
                        position += 8;
                        break;
                }
                var messageOverhead = position;
                var serializeBuffer = buffer.AsSpan(position);
                int written = 0;
                switch (messageCall.messageType)
                {
                    case MessageType.Send:
                    case MessageType.Request:
                    case MessageType.ReliableRequest:
                        written = service.RequestSerializers[messageCall.method](ref serializeBuffer, messageCall.message);
                        break;
                    case MessageType.Response:
                        written = service.ResponseSerializers[messageCall.method](ref serializeBuffer, messageCall.message);
                        break;
                }

                MemoryMarshal.Cast<byte, int>(buffer.AsSpan(0, 4)) [0] = written;
                int sendLength = messageOverhead + written;
                var sendBuffer = buffer.AsSpan(0, sendLength);
                socket.Send(sendBuffer, SocketFlags.None);
            }

            this.socket.Close();
        }

        private void Send(string method, object message, MessageType messageType, long messageId, Guid reliableId)
        {
            //Console.WriteLine($"Equeueing: {messageId}");
            sendQueue.Enqueue((method, message, messageType, messageId, reliableId));
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
            Send(method, message, MessageType.Send, messageId, Guid.Empty);
        }

        public async Task<TResult> Request<TRequest, TResult>(string method, TRequest request)
        {
            var messageId = Interlocked.Increment(ref this.nextMessageId);
            var completionSource = new TaskCompletionSource<object>(shutdownToken);
            messageCorrelationLookup.TryAdd(messageId, completionSource);
            Send(method, request, MessageType.Request, messageId, Guid.Empty);
            return (TResult) (await completionSource.Task);
        }

        public async Task<ReliableCompleter<TResult>> ReliableRequest<TRequest, TResult>(string method, TRequest request)
        {

            var db = service.GetReliableRequestDatabase(method);
            var requestsCollection = db.GetCollection<ReliableMessage>("Requests");

            var reliableMessageId = Guid.NewGuid();

            var reliableMessage = new ReliableMessage
            {
                MessageId = reliableMessageId,
                Method = method,
                Request = request
            };

            var messageId = Interlocked.Increment(ref this.nextMessageId);
            var completionSource = new TaskCompletionSource<object>(shutdownToken);
            messageCorrelationLookup.TryAdd(messageId, completionSource);
            Send(method, request, MessageType.ReliableRequest, messageId, reliableMessageId);
            var result = (TResult) (await completionSource.Task);
            var completer = new ReliableCompleter<TResult>();
            completer.Result = result;
            completer.Complete = () =>
            {
                requestsCollection.Delete(x => x.MessageId == reliableMessageId);
            };
            return completer;
        }

        public IEnumerable<TRequest> GetMessagesToReprocess<TRequest>(string method)
        {
            var db = service.GetReliableRequestDatabase(method);
            var requestsCollection = db.GetCollection<ReliableMessage>("Requests");

            var results = new List<TRequest>();
            foreach (var request in requestsCollection.FindAll())
            {
                results.Add((TRequest) request.Request);
            }
            return results;
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
            //Console.WriteLine($"Sending Directory with {files.Count} file(s)");

            foreach (var fileToSend in files)
            {
                var relativeRemotePath = fileToSend.Remove(0, parentDirectory.Length + 1);
                await SendFile(fileToSend, relativeRemotePath);
            }

        }

        public async Task SendFile(string filePath, string remoteFilePath)
        {
            var chunkSize = 65_536;

            using(var fs = new System.IO.FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var fileLength = fs.Length;
                var toRead = fs.Length;
                byte[] buffer = new byte[Math.Min(chunkSize, toRead)];
                var offset = 0L;

                while (toRead > 0)
                {
                    var read = await fs.ReadAsync(buffer, 0, buffer.Length);

                    if (read == toRead)
                    {
                        // We are on the last read
                        Array.Resize(ref buffer, read);
                    }
                    //Console.WriteLine($"Waiting file chunk send starting:{offset} length:{buffer.Length} of {filePath}");
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    var fileSendResponse = await Request<FileChunkSend, FileChunkSendResponse>("SendFileChunk", new FileChunkSend
                        {
                            FilePath = remoteFilePath,
                                Offset = offset,
                                Data = buffer
                        });
                    Console.WriteLine($"{DateTime.Now} - Wrote {(int)(buffer.Length / 1024)}kb and took {sw.ElapsedMilliseconds}ms");
                    if (!fileSendResponse.Success)
                    {
                        Console.WriteLine($"Remote server returned an error when writing file: {fileSendResponse.ErrorMessage}");
                        throw new Exception($"Remote server returned an error when writing file: {fileSendResponse.ErrorMessage}");
                    }

                    offset += read;
                    toRead = fileLength - offset;
                }

            }
        }

    }
}