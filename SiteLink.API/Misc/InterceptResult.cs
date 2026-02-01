using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.API.Misc
{
    public readonly struct InterceptResult
    {
        public InterceptDecision Decision { get; }
        public ArraySegment<byte> Replacement { get; } // only for Replace / Defer(with new)

        private InterceptResult(InterceptDecision d, ArraySegment<byte> r)
        { Decision = d; Replacement = r; }

        public static InterceptResult Pass() => new(InterceptDecision.Pass, default);
        public static InterceptResult Drop() => new(InterceptDecision.Drop, default);
        public static InterceptResult Replace(ArraySegment<byte> bytes) => new(InterceptDecision.Replace, bytes);
        public static InterceptResult Defer(ArraySegment<byte> bytes = default) => new(InterceptDecision.Defer, bytes);
    }
}
