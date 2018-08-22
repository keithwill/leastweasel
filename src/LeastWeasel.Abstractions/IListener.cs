using System.Threading;
using System.Threading.Tasks;

namespace LeastWeasel.Abstractions
{
    public interface IListener
    {
        Task RunAsync (CancellationToken cancellationToken);
    }
}