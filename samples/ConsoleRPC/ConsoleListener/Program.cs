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

            var requestSerializer = new Serializer<Request>()
                .Field(x => x.Id, (x, f) => x.Id = f)
                .Build();

            var responseSerializer = new Serializer<Response>()
                .Field(x => x.Id, (x, f) => x.Id = f)
                .Build();

            var response = new Response
            {
                Id = 1,
            };

            var service = new Service();

            service.RegisterRequestHandler<Request, Response>("TestRPC", async (x) =>
            {
                //await Task.Delay(10);
                return response;
            },
            (ref Span<byte> x) => requestSerializer.Deserialize(ref x),
            (ref Span<byte> x, object value) => responseSerializer.Serialize((Response)value, ref x)
            );

            var listener = new Listener(service);
            // Console.WriteLine("Starting to Listen");
            await listener.RunAsync(CancellationToken.None);

        }
    }
}