using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using LeastWeasel.Messaging;
using ModelTest;

namespace ClientTest
{
    class Program
    {
        static async Task Main(string[] args)
        {

            var requestSerializer = new Serializer<Request>()
                .Field(x => x.Id, (x, f) => x.Id = f)
                .Build();

            var responseDeserializer = new Serializer<Response>()
                .Field(x => x.Id, (x, f) => x.Id = f)
                .Build();

            var service = new Service();
            service.RegisterRequest<Request, Response>("TestRPC",
                (ref Span<byte> x, object value) => requestSerializer.Serialize((Request)value, ref x),
                (ref Span<byte> x) => responseDeserializer.Deserialize(ref x)
            );

            using (var client = new RoundRobinClient("localhost", service))
            {
                await client.ConnectAsync();

                var request = new Request
                {
                    Id = 1,
                };

                Stopwatch sw = new Stopwatch();

                sw.Start();

                var numberRequests = 10000;
                var tasks = new Task[5000];
                for (int req = 0; req < numberRequests; req++)
                {
                    for (int i = 0; i < tasks.Length; i++)
                    {
                        //Console.WriteLine($"Sending task {i}");
                        tasks[i] = client.Request<Request, Response>("TestRPC", request);

                    }
                    await Task.WhenAll(tasks);
                    //Console.WriteLine("Did 10");
                }

                sw.Stop();
                var elapsed = sw.Elapsed;

                Console.WriteLine($"Took {elapsed} for {numberRequests * tasks.Length} roundtrips at {(numberRequests * tasks.Length) / elapsed.TotalSeconds} reqs/sec");
            }

        }
    }
}