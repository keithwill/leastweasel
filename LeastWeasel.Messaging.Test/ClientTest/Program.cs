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
            service.Register<Request, Response>("TestRPC");      

            using (var client = new Client("localhost", service))
            {
                await client.ConnectAsync();
                //Console.WriteLine("Connected");

                var request = new Request
                {
                    Id = 1,
                    Name = "Sample Request",
                    Email = "test@test.com",
                    Details = new List<RequestDetail>
                    {
                        new RequestDetail
                        {
                            Id = 1,
                            Name = "Detail1",
                            Number = 1
                        },
                        new RequestDetail
                        {
                            Id = 2,
                            Name = "Detail2",
                            Number = 2
                        }
                    }
                };

                Stopwatch sw = new Stopwatch();
                
                sw.Start();

                var concurrency = 300;
                var requests = 10000;

                Task[] tasks = new Task[concurrency];
                for(int j = 0; j < tasks.Length; j++)
                {
                    tasks[j] = Task.Run(async () => {
                        for(int i = 0; i < requests; i++)
                        {
                            _ = await client.Request<Request, Response>("TestRPC", request);
                        }
                    });
                }

                await Task.WhenAll(tasks);

                sw.Stop();
                var elapsed = sw.Elapsed;
                Console.WriteLine($"Took {elapsed} for {concurrency * requests} roundtrips");
            }



        }
    }
}
