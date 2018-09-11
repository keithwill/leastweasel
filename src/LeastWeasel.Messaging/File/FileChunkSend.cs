using MessagePack;

namespace LeastWeasel.Messaging.File
{
    [MessagePackObject]
    public class FileChunkSend
    {
        [Key(1)]
        public string FilePath { get; set; }
        [Key(2)]
        public long Offset { get; set; }
        [Key(3)]
        public byte[] Data { get; set; }
        [Key(4)]
        public bool EndOfFile { get; set; }
    }
}