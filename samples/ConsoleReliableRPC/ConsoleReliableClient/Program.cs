using System;
using System.Diagnostics;
using System.Threading.Tasks;

using ConsoleReliableModel;

using LeastWeasel.Messaging;

namespace ConsoleReliableClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var requestSerializer = new Serializer<Request>()
                .Field(x => x.Message, (x, f) => x.Message = f)
                .Build();

            var responseSerializer = new Serializer<Response>()
                .Field(x => x.ResponseMessage, (x, f) => x.ResponseMessage = f)
                .Build();

            var service = new Service();
            service.RegisterRequest<Request, Response>("TestRPC",
                (ref Span<byte> x, object value) => requestSerializer.Serialize((Request) value, ref x),
                (ref Span<byte> x) => responseSerializer.Deserialize(ref x)
            );

            using(var client = new RoundRobinClient("localhost", service))
            {
                await client.ConnectAsync();

                var request = new Request
                {
                    Message = "Test Message"
                };

                Stopwatch sw = new Stopwatch();

                sw.Start();

                var numberRequests = 10000;
                for (int i = 0; i < numberRequests; i++)
                {
                    var completer = await client.ReliableRequest<Request, Response>("TestRPC", request);
                    completer.Complete();
                }

                sw.Stop();
                var elapsed = sw.Elapsed;

                Console.WriteLine($"Took {elapsed} for {numberRequests} roundtrips at {numberRequests / elapsed.TotalSeconds} reqs/sec");
            }
        }
    }
}