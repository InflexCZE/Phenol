using System;
using System.Collections.Generic;
using System.Text;

namespace LiveLink.Messages
{
    public class HitpointsRequest : Message
    {
        public long Target;
    }

    public struct HitPoint : IEquatable<HitPoint>
    {
        public int Class;
        public int Method;
        public int NodeIndex;
        public int PinIndex;

        public HitPoint(int @class, int method, int reference, int pinIndex)
        {
            this.Class = @class;
            this.Method = method;
            this.NodeIndex = reference;
            this.PinIndex = pinIndex;
        }

        public static bool operator==(HitPoint a, HitPoint b)
        {
            return a.Equals(b);
        }

        public static bool operator!=(HitPoint a, HitPoint b)
        {
            return !(a == b);
        }

        public bool Equals(HitPoint other)
        {
            return (this.Class, this.Method, this.NodeIndex, this.PinIndex) == (other.Class, other.Method, other.NodeIndex, other.PinIndex);
        }

        public override bool Equals(object obj)
        {
            return obj is HitPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (this.Class, this.Method, this.NodeIndex, this.PinIndex).GetHashCode();
        }
    }

    [Request(typeof(HitpointsRequest))]
    public class HitPoints : Message
    {
        public long DebugTarget;
        public List<HitPoint> Ids;

        public override bool IsReliable => false;
    }
}
