using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using LeastWeasel.Abstractions;
using LeastWeasel.Messaging.File;
using LiteQueue;

using MessagePack;

namespace LeastWeasel.Messaging
{

    public class Service : IService
    {
        public string Name { get; set; }
        public Dictionary<string, Func<ArraySegment<byte>, object>> RequestDeserializers;
        public Dictionary<string, Func<ArraySegment<byte>, object>> ResponseDeserializers;
        public Dictionary<string, Func<object, Task<object>>> RequestHandlers;

        public Dictionary<string, Func<object, Task>> SendHandlers;
        private MD5CryptoServiceProvider md5;
        public Dictionary<long, string> HashMethodLookup;
        public Dictionary<string, long> MethodHashLookup;

        public Dictionary<string, LiteDB.LiteDatabase> ReliableRequestDatabases;

        internal string PreSharedKey { get; set; }

        public string FileStagingDirectory = "staging";

        public Service()
        {
            this.RequestDeserializers = new Dictionary<string, Func<ArraySegment<byte>, object>>();
            this.ResponseDeserializers = new Dictionary<string, Func<ArraySegment<byte>, object>>();
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

            RegisterRequest<FileChunkSend, FileChunkSendResponse>("SendFileChunk");
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

                    using (var fs = new FileStream(filePath, fileMode, FileAccess.ReadWrite, FileShare.Read))
                    {
                        fs.Seek(request.Offset, SeekOrigin.Begin);
                        await fs.WriteAsync(request.Data, 0, request.Data.Length);
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
            string method, Func<TRequest, Task<TResponse>> handler
            )
        {
            RequestDeserializers.Add(method, (x) => MessagePackSerializer.Deserialize<TRequest>(x));
            RequestHandlers.Add(method, async (message) => { return await handler((TRequest)message); });
            AddMethodHash(method);
            return this;
        }

        public IService RegisterSendHandler<TRequest>(
            string method, Func<TRequest, Task> handler
            )
        {
            RequestDeserializers.Add(method, (x) => MessagePackSerializer.Deserialize<TRequest>(x));
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

        public IService RegisterRequest<TRequest, TResponse>(string method)
        {
            RequestDeserializers.Add(method, (x) => MessagePackSerializer.Deserialize<TRequest>(x));
            ResponseDeserializers.Add(method, (x) => MessagePackSerializer.Deserialize<TResponse>(x));
            AddMethodHash(method);
            return this;
        }

        public IService RegisterSend<TRequest>(string method)
        {
            RequestDeserializers.Add(method, (x) => MessagePackSerializer.Deserialize<TRequest>(x));
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

    }

}