using System;

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
