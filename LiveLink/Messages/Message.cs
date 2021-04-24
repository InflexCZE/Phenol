using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace LiveLink.Messages
{
    public abstract class Message
    {
        public Guid MsgId;
        
        /// <summary>
        /// Collection is closed when unreliable message fails to be delivered
        /// </summary>
        [JsonIgnore]
        public virtual bool IsReliable => true;

        protected Message()
        {
            this.MsgId = Guid.NewGuid();
        }
    }
}
