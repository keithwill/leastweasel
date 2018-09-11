using System.Threading;
using System.Threading.Tasks;

namespace LeastWeasel.Abstractions
{
    public interface IServer
    {
        Task RunAsync(CancellationToken cancellationToken);
    }
}