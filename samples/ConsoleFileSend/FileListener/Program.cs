using System;
using System.Threading;
using System.Threading.Tasks;
using LeastWeasel.Messaging;

namespace FileListener
{
    class Program
    {
        static async Task Main(string[] args)
        {

            var service = new Service();
            var listener = new Listener(service);
            await listener.RunAsync(CancellationToken.None);
            
        }
    }
}
