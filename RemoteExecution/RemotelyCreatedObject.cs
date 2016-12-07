using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RemoteExecution
{
    internal class RemotelyCreatedObject
    {
        public int RefCount;
        public object Value { get; set; }
    }
}
