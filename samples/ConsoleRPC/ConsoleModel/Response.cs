using System;
using System.Collections.Generic;
using MessagePack;

namespace ModelTest
{
    [MessagePackObject]
    public class Response
    {

        [Key(0)]
        public int Id { get; set; }
        // [Key(1)]
        // public int RequestId { get; set; }
        // [Key(2)]
        // public string Acknowledgement { get; set; }


        // [Key(3)]
        // public List<string> RequestErrors { get; set; }

        // [Key(4)]
        // public List<ResponseDetail> Details { get; set; }

    }
}