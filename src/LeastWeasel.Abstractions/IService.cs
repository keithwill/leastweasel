using System;
using System.Threading.Tasks;

namespace LeastWeasel.Abstractions
{
    public interface IService
    {


        IService RegisterRequestHandler<TRequest, TResponse>(
            string method,
            Func<TRequest, Task<TResponse>> handler,
            ServiceDelegates.SpanDeserializer requestDeserializer,
            ServiceDelegates.SpanSerializer responseSerializer
        );

        IService RegisterRequest<TRequest, TResponse>(
            string method,
            ServiceDelegates.SpanSerializer requestSerializer,
            ServiceDelegates.SpanDeserializer responseDeserializer
        );

        IService RegisterSend<TRequest>(string method, ServiceDelegates.SpanSerializer requestSerializer);

    }

    public class ServiceDelegates
    {
        public delegate int SpanSerializer(ref Span<byte> buffer, object value);
        public delegate object SpanDeserializer(ref Span<byte> buffer);
    }


}