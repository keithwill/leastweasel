using System;
using System.Collections.Generic;
using MessagePack;

namespace ModelTest
{
    [MessagePackObject]
    public class ResponseDetail
    {

        [Key(0)]
        public int Id {get;set;}
        [Key(1)]
        public string Name {get;set;}
        [Key(2)]
        public int Number {get;set;}
        [Key(3)]
        public List<string> DetailErrors {get;set;}

    }
}