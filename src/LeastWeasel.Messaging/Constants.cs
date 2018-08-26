namespace LeastWeasel.Messaging
{
    public static class Constants
    {
        public const int SEND_HEADER_SIZE = (4 + 1 + 8);                    // Length + Type + Method
        public const int REQUEST_HEADER_SIZE = (4 + 1 + 8 + 8);             // Length + Type + Method + Id
        public const int RELIABLE_REQUEST_HEADER_SIZE = (4 + 1 + 8 + 16);   // Length + Type + Method + Guid
    }
}
