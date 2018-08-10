using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

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

        public void Dispose()
        {
            if (!disposed)
            {
                foreach(var connection in connections)
                {
                    connection?.Dispose();
                }
                connectionCanceler.Cancel();
            }
        }
    }


}
