using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LeastWeasel.Messaging;
using ModelTest;

namespace ListenerTest
{
    class Program
    {

        static async Task Main(string[] args)
        {
            var response = new Response
            {
                Id = 1,
                // Acknowledgement = "alkenofneofn",
                // Details = new List<ResponseDetail> { },
                // RequestErrors = new List<string> { "alknfeoinf", "woinegowing" },
                // RequestId = 1
            };

            var service = new Service();
            service.RegisterRequestHandler<Request, Response>("TestRPC", async (x) =>
            {
                //await Task.Delay(10);
                return response;
            });

            var listener = new Listener(service);
            // Console.WriteLine("Starting to Listen");
            await listener.RunAsync(CancellationToken.None);

        }
    }
}