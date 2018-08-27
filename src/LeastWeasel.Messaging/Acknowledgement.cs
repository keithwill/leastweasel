using System;

namespace LeastWeasel.Messaging
{
    public class Acknowledgement
    {
        public string Method;
        public Guid RequestId;
        public Guid ResponseId;
    }
}