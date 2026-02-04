namespace SiteLink.API.Structs
{
    public readonly struct InterceptResult
    {
        public InterceptDecision Decision { get; }
        public ArraySegment<byte> Replacement { get; }

        private InterceptResult(InterceptDecision d, ArraySegment<byte> r)
        { Decision = d; Replacement = r; }

        public static InterceptResult Pass() => new(InterceptDecision.Pass, default);
        public static InterceptResult Drop() => new(InterceptDecision.Drop, default);
        public static InterceptResult Replace(ArraySegment<byte> bytes) => new(InterceptDecision.Replace, bytes);
        public static InterceptResult Defer(ArraySegment<byte> bytes = default) => new(InterceptDecision.Defer, bytes);
    }
}
