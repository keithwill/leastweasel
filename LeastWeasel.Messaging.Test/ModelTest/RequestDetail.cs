
using System;
using MessagePack;

namespace ModelTest
{

    [MessagePackObject]
    public class RequestDetail
    {
        
        [Key(0)]
        public int Id {get;set;}
        [Key(1)]
        public int Number {get;set;}

        [Key(2)]
        public string Name {get;set;}

    }

}
