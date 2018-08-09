using System;
using System.Collections.Generic;
using MessagePack;

namespace ModelTest
{
    
    [MessagePackObject]
    public class Request
    {

        [Key(0)]
        public int Id {get;set;}
        [Key(1)]
        public string Name {get;set;}
        [Key(2)]
        public string Email {get;set;}

        [Key(3)]
        public List<RequestDetail> Details {get;set;}
    }


}
