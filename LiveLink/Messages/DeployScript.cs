using System;
using System.Collections.Generic;
using System.Text;

namespace LiveLink.Messages
{
    public class DeployScript : Message
    {
        public string Code;
        public long DebugTarget;
    }
}
