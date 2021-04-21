using System;
using System.Collections.Generic;
using System.Text;

namespace LiveLink.Messages
{
    public abstract class Message
    {
        public Guid MsgId;

        protected Message()
        {
            this.MsgId = Guid.NewGuid();
        }
    }
}
