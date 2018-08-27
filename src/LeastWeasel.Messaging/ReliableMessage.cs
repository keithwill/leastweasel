using System;

namespace LeastWeasel.Messaging
{
    public class ReliableMessage
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public string Method { get; set; }
        public object Request { get; set; }
    }

}