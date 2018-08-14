
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MessagePack;
using LeastWeasel.Messaging.File;
using System.Text;
using System.IO;

namespace LeastWeasel.Messaging
{

    public class Service
    {
        public string Name {get;set;}
        public Dictionary<string, Func<ArraySegment<byte>, object>> RequestDeserializers;
        public Dictionary<string, Func<ArraySegment<byte>, object>> ResponseDeserializers;
        public Dictionary<string, Func<object, Task<object>>> Handlers;
        private MD5CryptoServiceProvider md5;
        public Dictionary<long, string> HashMethodLookup;
        public Dictionary<string, long> MethodHashLookup;

        internal string PreSharedKey {get;set;}

        public string FileStagingDirectory = "staging";

        public Service()
        {
            this.RequestDeserializers = new Dictionary<string, Func<ArraySegment<byte>, object>>();
            this.ResponseDeserializers = new Dictionary<string, Func<ArraySegment<byte>, object>>();
            this.Handlers = new Dictionary<string, Func<object, Task<object>>>();
            this.HashMethodLookup = new Dictionary<long, string>();
            this.md5 = new MD5CryptoServiceProvider();
            this.MethodHashLookup = new Dictionary<string, long>();
            RegisterFileHandlers();
        }

        public Service(string preSharedKey) : base() {
            this.PreSharedKey = preSharedKey;
        }

        private void RegisterFileHandlers()
        {

            string fullStagingDirectory = System.IO.Path.GetFullPath(FileStagingDirectory);

            Register<FileChunkSend, FileChunkSendResponse>("SendFileChunk");
            Handlers.Add("SendFileChunk", async (message) => {
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
                catch(Exception ex)
                {
                    return new FileChunkSendResponse
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }

            });
        }

        public Service RegisterHandler<TRequest, TResponse>(string method, Func<TRequest, Task<TResponse>> handler)
        {
            RequestDeserializers.Add(method, (x) => LZ4MessagePackSerializer.Deserialize<TRequest>(x));
            Handlers.Add(method, async (message) => { return await handler((TRequest)message); });
            AddMethodHash(method);
            return this;
        }

        private void AddMethodHash(string method)
        {
            var methodHash = method.ComputeMD5HashAsInt64();
            HashMethodLookup.Add(methodHash, method);
            MethodHashLookup.Add(method, methodHash);
        }

        public Service Register<TRequest, TResponse>(string method)
        {
            RequestDeserializers.Add(method, (x) => LZ4MessagePackSerializer.Deserialize<TRequest>(x));
            ResponseDeserializers.Add(method, (x) => LZ4MessagePackSerializer.Deserialize<TResponse>(x));
            AddMethodHash(method);
            return this;
        }

        public Service Register<TRequest>(string method)
        {
            RequestDeserializers.Add(method, (x) => LZ4MessagePackSerializer.Deserialize<TRequest>(x));
            AddMethodHash(method);
            return this;
        }
        
        /// <summary>
        /// If set, this service will not respond to requets unless the client is using the same
        /// pre shared key. This does not encrypt traffic.
        /// </summary>
        /// <param name="key"></param>
        public void RequirePreSharedKey(string key)
        {
            PreSharedKey = key;
        }

    }

}