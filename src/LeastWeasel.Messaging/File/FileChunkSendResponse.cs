using MessagePack;

namespace LeastWeasel.Messaging.File
{
    [MessagePackObject]
    public class FileChunkSendResponse
    {
        [Key(0)]
        public bool Success { get; set; }
        [Key(1)]
        public string ErrorMessage { get; set; }
    }
}