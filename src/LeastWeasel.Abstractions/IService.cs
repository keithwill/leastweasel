using System;
using System.Threading.Tasks;

namespace LeastWeasel.Abstractions
{
    public interface IService
    {
        IService RegisterRequestHandler<TRequest, TResponse> (string method, Func<TRequest, Task<TResponse>> handler);
        IService RegisterRequest<TRequest, TResponse> (string method);
        IService RegisterSend<TRequest> (string method);
    }
}