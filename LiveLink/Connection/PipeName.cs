using System;
using System.Collections.Generic;
using System.Text;

namespace LiveLink.Connection
{
    public enum Side
    {
        In,
        Out
    }

    public readonly struct PipeName
    {
        private readonly Side Side;
        private readonly string Name;

        public PipeName(string name, Side side)
        {
            this.Side = side;
            this.Name = name;
        }

        public string Read => Side == Side.In ? $"In-{Name}" : $"Out-{Name}";

        public string Write => Side == Side.In ? $"Out-{Name}" : $"In-{Name}";
    }
}
