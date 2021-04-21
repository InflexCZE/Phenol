using System;
using System.Collections.Generic;
using System.Text;

namespace LiveLink.Messages
{
    public class ListDebugTargets : Request
    { }

    [Request(typeof(ListDebugTargets))]
    public class DebugTargets : Response
    {
        public struct DebugTarget
        {
            public long Id;
            public string Name;
        }

        public List<DebugTarget> Targets { get; set; }
    }
}
