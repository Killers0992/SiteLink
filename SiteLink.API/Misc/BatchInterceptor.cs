namespace SiteLink.API.Misc
{
    public delegate InterceptResult MessageHandler(ushort id, NetworkReader reader, ArraySegment<byte> original, Session session);

    public sealed class BatchInterceptor
    {
        public PacketDirection Direction { get; }

        private readonly List<MessageHandler>?[] _handlers = new List<MessageHandler>?[ushort.MaxValue + 1];

        public BatchInterceptor(PacketDirection direction)
        {
            Direction = direction;
        }

        public void Register(ushort id, MessageHandler handler)
        {
            List<MessageHandler> handlers = _handlers[id] ??= new List<MessageHandler>();

            lock (handlers)
            {
                if (!handlers.Contains(handler))
                    handlers.Add(handler);
            }
        }

        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            return bytes switch
            {
                >= GB => $"{bytes / (double)GB:F2} GB",
                >= MB => $"{bytes / (double)MB:F2} MB",
                >= KB => $"{bytes / (double)KB:F2} KB",
                _ => $"{bytes} B"
            };
        }

        public bool TryRewrite(
            Session session,
            byte[] src, int srcOffset, int srcLength,
            out byte[] dst, out int dstOffset, out int dstLength,
            out bool pooled)
        {
            dst = src; dstOffset = srcOffset; dstLength = srcLength; pooled = false;

            var batch = new ArraySegment<byte>(src, srcOffset, srcLength);
            var r = new NetworkReader(batch);

            if (r.Remaining < sizeof(double)) return false;
            double ts = r.ReadDouble();

            // only allocate lists if we actually need to change anything
            List<ArraySegment<byte>> kept = null;
            List<ArraySegment<byte>> deferred = null;

            bool changed = false;

            while (r.Remaining > 0)
            {
                int size = (int)Compression.DecompressVarUInt(r);
                if (r.Remaining < size) break;

                ArraySegment<byte> msg = r.ReadBytesSegment(size);

                NetworkReader mr = new NetworkReader(msg);
                if (!Mirror.NetworkMessages.UnpackId(mr, out ushort id))
                {
                    kept ??= new(16);
                    kept.Add(msg);
                    continue;
                }

                /*switch (id)
                {

                    case NetworkMessages.NetworkPongMessage:
                    case NetworkMessages.NetworkPingMessage:
                    case NetworkMessages.FpcPositionMessage:
                    case NetworkMessages.TimeSnapshotMessage:
                        break;
                    default:
                        SiteLinkLogger.Info(NetworkMessages.GetMessageName(id) + $" {FormatBytes(size)} ", Direction.ToString());
                        break;
                }*/

                List<MessageHandler> handlers = _handlers[id];
                if (handlers == null)
                {
                    kept ??= new(16);
                    kept.Add(msg);
                    continue;
                }

                MessageHandler[] handlersSnapshot;
                lock (handlers)
                    handlersSnapshot = handlers.ToArray();

                InterceptResult res = InterceptResult.Pass();

                foreach (MessageHandler handler in handlersSnapshot)
                {
                    NetworkReader handlerReader = new NetworkReader(msg);
                    if (!Mirror.NetworkMessages.UnpackId(handlerReader, out _))
                        continue;

                    res = handler(id, handlerReader, msg, session);

                    if (res.Decision != InterceptDecision.Pass)
                        break;
                }

                switch (res.Decision)
                {
                    case InterceptDecision.Pass:
                        kept ??= new(16);
                        kept.Add(msg);
                        break;

                    case InterceptDecision.Drop:
                        changed = true;
                        break;

                    case InterceptDecision.Replace:
                        changed = true;
                        kept ??= new(16);
                        kept.Add(res.Replacement);
                        break;

                    case InterceptDecision.Defer:
                        changed = true;
                        deferred ??= new(8);
                        deferred.Add(res.Replacement.Array != null ? res.Replacement : msg);
                        break;
                }
            }

            if (!changed) return false;

            kept ??= new(0);
            if (deferred != null) kept.AddRange(deferred);

            // compute output size
            int outSize = sizeof(double);
            foreach (var seg in kept)
                outSize += Compression.VarUIntSize((ulong)seg.Count) + seg.Count;

            // rent output
            dst = System.Buffers.ArrayPool<byte>.Shared.Rent(outSize);
            pooled = true;
            dstOffset = 0;

            // write
            int p = 0;
            WriteDouble(dst, ref p, ts);

            foreach (var seg in kept)
            {
                // Write length prefix (varuint)
                WriteVarUInt(dst, ref p, (uint)seg.Count);

                // Copy message bytes
                Buffer.BlockCopy(seg.Array!, seg.Offset, dst, p, seg.Count);
                p += seg.Count;
            }

            dstLength = p;
            return true;
        }

        private static void WriteVarUInt(byte[] buffer, ref int pos, uint value)
        {
            while (value >= 0x80)
            {
                buffer[pos++] = (byte)(value | 0x80);
                value >>= 7;
            }
            buffer[pos++] = (byte)value;
        }


        private static void WriteDouble(byte[] buffer, ref int pos, double value)
        {
            ulong bits = (ulong)BitConverter.DoubleToInt64Bits(value);
            buffer[pos++] = (byte)bits;
            buffer[pos++] = (byte)(bits >> 8);
            buffer[pos++] = (byte)(bits >> 16);
            buffer[pos++] = (byte)(bits >> 24);
            buffer[pos++] = (byte)(bits >> 32);
            buffer[pos++] = (byte)(bits >> 40);
            buffer[pos++] = (byte)(bits >> 48);
            buffer[pos++] = (byte)(bits >> 56);
        }
    }
}
