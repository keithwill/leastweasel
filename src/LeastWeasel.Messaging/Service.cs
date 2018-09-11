using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using LeastWeasel.Abstractions;
using LeastWeasel.Messaging.File;

using MessagePack;

namespace LeastWeasel.Messaging
{

    public class Service : IService
    {
        public string Name { get; set; }



        public Dictionary<string, ServiceDelegates.SpanDeserializer> RequestDeserializers;
        public Dictionary<string, ServiceDelegates.SpanDeserializer> ResponseDeserializers;

        public Dictionary<string, ServiceDelegates.SpanSerializer> RequestSerializers;
        public Dictionary<string, ServiceDelegates.SpanSerializer> ResponseSerializers;

        public Dictionary<string, Func<object, Task<object>>> RequestHandlers;

        public Dictionary<string, Func<object, Task>> SendHandlers;
        private MD5CryptoServiceProvider md5;
        public Dictionary<long, string> HashMethodLookup;
        public Dictionary<string, long> MethodHashLookup;

        public int MaxMessageSize = 1_000_000;

        private object initializeReliableDatabaseLock = new object();

        public Dictionary<string, LiteDB.LiteDatabase> ReliableRequestDatabases;

        internal string PreSharedKey { get; set; }

        public string FileStagingDirectory = "staging";

        public Service()
        {
            this.RequestDeserializers = new Dictionary<string, ServiceDelegates.SpanDeserializer>();
            this.ResponseDeserializers = new Dictionary<string, ServiceDelegates.SpanDeserializer>();
            this.RequestSerializers = new Dictionary<string, ServiceDelegates.SpanSerializer>();
            this.ResponseSerializers = new Dictionary<string, ServiceDelegates.SpanSerializer>();
            this.RequestHandlers = new Dictionary<string, Func<object, Task<object>>>();
            this.SendHandlers = new Dictionary<string, Func<object, Task>>();
            this.HashMethodLookup = new Dictionary<long, string>();
            this.md5 = new MD5CryptoServiceProvider();
            this.MethodHashLookup = new Dictionary<string, long>();
            this.ReliableRequestDatabases = new Dictionary<string, LiteDB.LiteDatabase>();
            RegisterFileHandlers();
        }

        private void RegisterFileHandlers()
        {

            string fullStagingDirectory = System.IO.Path.GetFullPath(FileStagingDirectory);

            var fileChunkSendSerializer = FileSerializers.GetFileChunkSend();
            var fileChunkSendResponseSerializer = FileSerializers.GetFileChunkSendResponse();

            RegisterRequest<FileChunkSend, FileChunkSendResponse>(
                "SendFileChunk",
                (ref Span<byte> x, object value) => fileChunkSendSerializer.Serialize((FileChunkSend)value, ref x),
                (ref Span<byte> x) => fileChunkSendResponseSerializer.Deserialize(ref x)
            );

            RequestHandlers.Add("SendFileChunk", async (message) =>
            {
                var request = message as FileChunkSend;

                var filePath = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(fullStagingDirectory, request.FilePath)
                );

                if (!filePath.StartsWith(fullStagingDirectory))
                {
                    return new FileChunkSendResponse
                    {
                        Success = false,
                        ErrorMessage = "Path of provided FileName was invalid. " +
                            "Ensure the FileName does not contain relative directory directives. " +
                            "FileName: " + request.FilePath
                    };
                }

                var directory = System.IO.Path.GetDirectoryName(filePath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                try
                {
                    FileMode fileMode = request.Offset == 0 ? FileMode.Create : FileMode.Open;
                    //Console.WriteLine("Opening file " + filePath);
                    using (var fs = new FileStream(filePath, fileMode, FileAccess.ReadWrite, FileShare.Read))
                    {
                        fs.Seek(request.Offset, SeekOrigin.Begin);
                        //Console.WriteLine("Seeking file " + filePath);
                        await fs.WriteAsync(request.Data, 0, request.Data.Length);
                        Console.WriteLine($"{DateTime.Now} - Wrote {request.Data.Length} byte(s) to file " + filePath);
                    }
                    return new FileChunkSendResponse
                    {
                        Success = true
                    };
                }
                catch (Exception ex)
                {
                    return new FileChunkSendResponse
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }

            });
        }

        public IService RegisterRequestHandler<TRequest, TResponse>(
            string method,
            Func<TRequest, Task<TResponse>> handler,
            ServiceDelegates.SpanDeserializer requestDeserializer,
            ServiceDelegates.SpanSerializer responseSerializer
            )
        {
            RequestDeserializers.Add(method, requestDeserializer);
            ResponseSerializers.Add(method, responseSerializer);
            RequestHandlers.Add(method, async (message) => { return await handler((TRequest)message); });
            AddMethodHash(method);
            return this;
        }

        public IService RegisterSendHandler<TRequest>(
            string method, Func<TRequest, Task> handler, ServiceDelegates.SpanDeserializer requestDeserializer
            )
        {
            RequestDeserializers.Add(method, requestDeserializer);
            SendHandlers.Add(method, async (message) => { await handler((TRequest)message); });
            AddMethodHash(method);
            return this;
        }

        private void AddMethodHash(string method)
        {
            var methodHash = method.ComputeMD5HashAsInt64();
            HashMethodLookup.Add(methodHash, method);
            MethodHashLookup.Add(method, methodHash);
        }

        public IService RegisterRequest<TRequest, TResponse>(
            string method,
            ServiceDelegates.SpanSerializer requestSerializer,
            ServiceDelegates.SpanDeserializer responseDeserializer
            )
        {
            RequestSerializers.Add(method, requestSerializer);
            ResponseDeserializers.Add(method, responseDeserializer);
            AddMethodHash(method);
            return this;
        }

        public IService RegisterSend<TRequest>(string method, ServiceDelegates.SpanSerializer requestSerializer)
        {
            RequestSerializers.Add(method, requestSerializer);
            AddMethodHash(method);
            return this;
        }

        /// <summary>
        /// If set, this service will not respond to requets unless the client is using the same
        /// pre shared key. This does not encrypt traffic.
        /// </summary>
        /// <param name="key"></param>
        public IService RequirePreSharedKey(string key)
        {
            PreSharedKey = key;
            return this;
        }

        public LiteDB.LiteDatabase GetReliableRequestDatabase(string method)
        {
            LiteDB.LiteDatabase db;
            // Check for readonly access without exclusive lock first
            if (!ReliableRequestDatabases.TryGetValue(method, out db))
            {
                lock (initializeReliableDatabaseLock)
                {
                    if (!ReliableRequestDatabases.TryGetValue(method, out db))
                    {
                        db = new LiteDB.LiteDatabase(method + "Queue.db");
                        db.GetCollection<ReliableMessage>("Requests").EnsureIndex("MessageId");
                        ReliableRequestDatabases.Add(method, db);
                    }
                }
            }
            return db;
        }

    }

}