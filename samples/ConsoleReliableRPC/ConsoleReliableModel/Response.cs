using MessagePack;

namespace ConsoleReliableModel
{
    [MessagePackObject]
    public class Response
    {
        [Key(0)]
        public string ResponseMessage { get; set; }
    }
}