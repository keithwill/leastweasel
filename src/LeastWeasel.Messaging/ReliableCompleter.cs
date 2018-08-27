using System;

namespace LeastWeasel.Messaging
{
    public class ReliableCompleter<T>
    {
        public T Result;
        public Action Complete;
    }
}