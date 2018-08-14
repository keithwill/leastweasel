using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LeastWeasel.Messaging.File;

namespace LeastWeasel.Messaging
{
    public class Client : IDisposable
    {

        public Client(string hostName, Service service, int port = 8888)
        {
            this.hostName = hostName;
            this.service = service;
            this.port = port;
            this.connections = new Connection[3];
            this.nextConectionIndex = 0;
        }
        
        public async Task ConnectAsync()
        {
            this.connectionCanceler = new CancellationTokenSource();
            var tasks = new Task[this.connections.Length];
            for(int i = 0; i < this.connections.Length; i++)
            {
                var connection = new Connection(this);
                this.connections[i] = connection;
                tasks[i] = connection.ConnectAsync(connectionCanceler.Token);
            }
            await Task.WhenAll(tasks);
        }

        private readonly string hostName;
        private readonly Service service;
        private readonly int port;
        private long nextConectionIndex;
        private CancellationTokenSource connectionCanceler;

        public string HostName => this.hostName;
        public Service Service => this.service;
        public int Port => this.port;

        private Connection[] connections;
        private bool disposed;

        public void Send(string method, object message)
        {
            var index = Interlocked.Increment(ref nextConectionIndex) % connections.Length;
            connections[index].Send(method, message);
        }

        public async Task<TResult> Request<TRequest, TResult>(string method, TRequest request) where TResult : class
        {
            var index = Interlocked.Increment(ref nextConectionIndex) % connections.Length;
            return (await connections[index].Request(method, request)) as TResult;
        }

        public async Task SendDirectory(string directoryPath, string remoteDirectoryPath)
        {
            var directory = new System.IO.DirectoryInfo(directoryPath);
            var directoryFullPath = System.IO.Path.GetFullPath(directory.FullName);
            var parentDirectory = directory.Parent.FullName;
            var files = new List<string>();
            GetAllFiles(directory, files);

            for(int i = 0; i < files.Count; i+=4)
            {
                var filesToSend = Math.Min(4, files.Count - i);
                if (filesToSend > 0)
                {
                    var tasks = new Task[filesToSend];
                    for(int j = 0; j < filesToSend; j++)
                    {
                        var file = files[j];
                        var relativeRemotePath = file.Remove(0, parentDirectory.Length + 1);
                        tasks[j] = SendFile(file, relativeRemotePath);
                    }
                    await Task.WhenAll(tasks);
                }
            }
            
        }

        private void GetAllFiles(DirectoryInfo directory, List<string> files)
        {
            files.AddRange(directory.GetFiles().Select(x => x.FullName));
            foreach(var subdirectory in directory.GetDirectories())
            {
                GetAllFiles(subdirectory, files);
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

                    var fileSendResponse = await Request<FileChunkSend, FileChunkSendResponse>("SendFileChunk", new FileChunkSend{
                        FilePath = remoteFilePath,
                        Offset = offset,
                        Data = buffer
                    });
                    
                    offset += read;
                    toRead = fileLength - offset;
                }                

            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                foreach(var connection in connections)
                {
                    connection?.Dispose();
                }
                connectionCanceler.Cancel();
                disposed = true;
            }
        }
    }


}
