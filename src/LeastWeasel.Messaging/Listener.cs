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
            while (!cancellationToken.IsCancellationRequested)
            {
                var socket = await listenSocket.AcceptAsync();
                var connection = new Client(service, socket);
                connection.BeginMessaging();
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

    }

}