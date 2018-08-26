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
            var service = new Service();
            service.RegisterRequest<Request, Response>("TestRPC");

            using (var client = new RoundRobinClient("localhost", service))
            {
                await client.ConnectAsync();

                var request = new Request
                {
                    Id = 1,
                    // Name = "Sample Request",
                    // Email = "test@test.com",
                    // Details = new List<RequestDetail> {
                    // new RequestDetail {
                    // Id = 1,
                    // Name = "Detail1",
                    // Number = 1
                    // },
                    // new RequestDetail {
                    // Id = 2,
                    // Name = "Detail2",
                    // Number = 2
                    // }
                    // }
                };

                Stopwatch sw = new Stopwatch();

                sw.Start();

                var numberRequests = 10000;
                for (int i = 0; i < numberRequests; i++)
                {
                    await client.Request<Request, Response>("TestRPC", request);
                }

                sw.Stop();
                var elapsed = sw.Elapsed;

                Console.WriteLine($"Took {elapsed} for {numberRequests} roundtrips at {numberRequests / elapsed.TotalSeconds} reqs/sec");
            }

        }
    }
}