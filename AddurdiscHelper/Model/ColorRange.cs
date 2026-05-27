using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace AddurdiscHelper.Model
{
    internal class ColorRange
    {
        public Range R { get; }
        public Range G { get; }
        public Range B { get; }

        public ColorRange() : this(0,255) { }

        public ColorRange(int min, int max) : this(new Range(min, max), new Range(min, max), new Range(min, max)) { }

        public ColorRange(Range r, Range g, Range b)
        {
            R = r;
            G = g;
            B = b;
        }
    }
}
