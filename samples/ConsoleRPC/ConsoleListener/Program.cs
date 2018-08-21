using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LeastWeasel.Messaging;
using ModelTest;

namespace ListenerTest {
    class Program {
        static async Task Main (string[] args) {
            var service = new Service ();
            service.RegisterRequestHandler<Request, Response> ("TestRPC", async (x) => {
                await Task.Delay (1);
                return new Response {
                    Acknowledgement = "Returned result",
                        Id = x.Id,
                        RequestErrors = new List<string> (),
                        RequestId = x.Id,
                        Details = x.Details.Select (detail => new ResponseDetail {
                            Id = detail.Id,
                            Name = detail.Name,
                            Number = detail.Number,
                            DetailErrors = new List<string> { "Failed Validation 1", "Failed Validation 2" }
                            }).ToList ()
                };
            });

            var listener = new Listener (service);
            // Console.WriteLine("Starting to Listen");
            await listener.RunAsync (CancellationToken.None);

        }
    }
}