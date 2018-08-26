namespace LeastWeasel.Messaging
{
    public enum MessageType : byte
    {
        Send,
        Request,
        Response,
        ReliableRequest,
        ReliableResponse
    }
}