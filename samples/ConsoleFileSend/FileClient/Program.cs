using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using LeastWeasel.Messaging;

namespace FileClient
{
    class Program
    {
        static async Task Main(string[] args)
        {

            var service = new Service();
            Stopwatch sw = new Stopwatch();

            using (var client = new Client("localhost", service))
            {
                await client.ConnectAsync();
                sw.Start();
                await client.SendDirectory("Test", "Test");
            }
            sw.Stop();
            Console.WriteLine($"File send took {sw.Elapsed}");
            
        }
    }
}
