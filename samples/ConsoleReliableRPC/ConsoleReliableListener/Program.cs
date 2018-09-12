using System;
using System.Threading;
using System.Threading.Tasks;

using ConsoleReliableModel;

using LeastWeasel.Messaging;

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

            var requestSerializer = new Serializer<Request>()
                .Field(x => x.Message, (x, f) => x.Message = f)
                .Build();

            var responseSerializer = new Serializer<Response>()
                .Field(x => x.ResponseMessage, (x, f) => x.ResponseMessage = f)
                .Build();

            var service = new Service();
            service.RegisterRequestHandler<Request, Response>("TestRPC", async(x) =>
                {
                    //await Task.Delay(10);
                    return response;
                },
                (ref Span<byte> x) => requestSerializer.Deserialize(ref x),
                (ref Span<byte> x, object value) => responseSerializer.Serialize((Response) value, ref x)
            );

            var listener = new Listener(service);
            // Console.WriteLine("Starting to Listen");
            await listener.RunAsync(CancellationToken.None);
        }
    }
}