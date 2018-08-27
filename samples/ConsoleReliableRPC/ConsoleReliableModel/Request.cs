using System;
using MessagePack;

namespace ConsoleReliableModel
{
    [MessagePackObject]
    public class Request
    {
        [Key(0)]
        public string Message { get; set; }
    }
}
