using ConsoleReliableModel;
using LeastWeasel.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleReliableListener
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var response = new Response
            {
                ResponseMessage = "Response"
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
