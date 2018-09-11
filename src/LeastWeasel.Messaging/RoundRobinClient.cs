using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LeastWeasel.Abstractions;
using LeastWeasel.Messaging.File;

namespace LeastWeasel.Messaging
{
    public class RoundRobinClient : IDisposable, IClient
    {

        public RoundRobinClient(string hostName, Service service, int port = 8888)
        {
            this.hostName = hostName;
            this.service = service;
            this.port = port;
            this.connections = new Client[8];
            this.nextConectionIndex = 0;
        }

        public async Task ConnectAsync()
        {
            var tasks = new Task[this.connections.Length];
            for (int i = 0; i < this.connections.Length; i++)
            {
                var connection = new Client(service, hostName, port);
                this.connections[i] = connection;
                tasks[i] = connection.ConnectAsync();
            }
            await Task.WhenAll(tasks);
        }

        private readonly string hostName;
        private readonly Service service;
        private readonly int port;
        private long nextConectionIndex;

        public string HostName => this.hostName;
        public Service Service => this.service;
        public int Port => this.port;

        private Client[] connections;
        private bool disposed;

        public void Send<TRequest>(string method, TRequest message)
        {
            var index = Interlocked.Increment(ref nextConectionIndex) % connections.Length;
            connections[index].Send(method, message);
        }

        public async Task<TResult> Request<TRequest, TResult>(string method, TRequest request)
        {
            var index = Interlocked.Increment(ref nextConectionIndex) % connections.Length;
            return await connections[index].Request<TRequest, TResult>(method, request);
        }

        public async Task<ReliableCompleter<TResult>> ReliableRequest<TRequest, TResult>(string method, TRequest request)
        {
            var index = Interlocked.Increment(ref nextConectionIndex) % connections.Length;
            return await connections[index].ReliableRequest<TRequest, TResult>(method, request);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                foreach (var connection in connections)
                {
                    connection?.Dispose();
                }
                disposed = true;
            }
        }

        public async Task SendFile(string filePath, string remoteFilePath)
        {
            var index = Interlocked.Increment(ref nextConectionIndex) % connections.Length;
            await connections[index].SendFile(filePath, remoteFilePath);
        }

        public async Task SendDirectory(string directoryPath, string remoteDirectoryPath)
        {
            var index = Interlocked.Increment(ref nextConectionIndex) % connections.Length;
            await connections[index].SendDirectory(directoryPath, remoteDirectoryPath);
        }

    }

}