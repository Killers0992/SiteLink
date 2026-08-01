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

                switch (id)
                {

                    case NetworkMessages.NetworkPongMessage:
                    case NetworkMessages.NetworkPingMessage:
                    case NetworkMessages.FpcPositionMessage:
                    case NetworkMessages.TimeSnapshotMessage:
                        break;
                    default:
                        if (SiteLinkSettings.Singleton != null && SiteLinkSettings.Singleton.DebugMode)
                        {
                            SiteLinkLogger.Info(NetworkMessages.GetMessageName(id) + $" {FormatBytes(size)} ", Direction.ToString());
                        }
                        break;
                }

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
                // Write length prefix (Mirror's variable-length encoding)
                WriteVarUInt(dst, ref p, (ulong)seg.Count);

                // Copy message bytes
                Buffer.BlockCopy(seg.Array!, seg.Offset, dst, p, seg.Count);
                p += seg.Count;
            }

            dstLength = p;
            return true;
        }

        /// <summary>
        /// Writes a length prefix in Mirror's variable-length encoding.
        /// <para>
        /// This is deliberately not LEB128. Mirror uses the SQLite4 style scheme, where
        /// anything up to 240 fits in a single byte and the ranges above it are keyed off a
        /// marker byte. An LEB128 encoder agrees with it only for values below 128, so it
        /// silently produced correct batches until a rewritten message first grew past that -
        /// then the client read the length prefix as one byte, treated the continuation byte
        /// as message data, and every message after it in the batch was shifted by one. Mirror
        /// throws on the garbage and closes the connection with no kick reason, which is
        /// exactly what appending the server selector to the settings pack triggered.
        /// </para>
        /// <para>
        /// Kept byte-for-byte identical to <c>Mirror.Compression.CompressVarUInt</c>; the
        /// round-trip against Mirror's own decoder is covered by a test.
        /// </para>
        /// </summary>
        internal static void WriteVarUInt(byte[] buffer, ref int pos, ulong value)
        {
            if (value <= 240)
            {
                buffer[pos++] = (byte)value;
            }
            else if (value <= 2287)
            {
                buffer[pos++] = (byte)(((value - 240) >> 8) + 241);
                buffer[pos++] = (byte)((value - 240) & 0xFF);
            }
            else if (value <= 67823)
            {
                buffer[pos++] = 249;
                buffer[pos++] = (byte)((value - 2288) >> 8);
                buffer[pos++] = (byte)((value - 2288) & 0xFF);
            }
            else if (value <= 16777215)
            {
                buffer[pos++] = 250;
                buffer[pos++] = (byte)value;
                buffer[pos++] = (byte)(value >> 8);
                buffer[pos++] = (byte)(value >> 16);
            }
            else if (value <= uint.MaxValue)
            {
                buffer[pos++] = 251;
                WriteUInt32(buffer, ref pos, (uint)value);
            }
            else if (value <= 1099511627775UL)
            {
                buffer[pos++] = 252;
                buffer[pos++] = (byte)value;
                WriteUInt32(buffer, ref pos, (uint)(value >> 8));
            }
            else if (value <= 281474976710655UL)
            {
                buffer[pos++] = 253;
                buffer[pos++] = (byte)value;
                buffer[pos++] = (byte)(value >> 8);
                WriteUInt32(buffer, ref pos, (uint)(value >> 16));
            }
            else if (value <= 72057594037927935UL)
            {
                buffer[pos++] = 254;
                for (int i = 0; i < 7; i++)
                    buffer[pos++] = (byte)(value >> (i * 8));
            }
            else
            {
                buffer[pos++] = byte.MaxValue;
                for (int i = 0; i < 8; i++)
                    buffer[pos++] = (byte)(value >> (i * 8));
            }
        }

        private static void WriteUInt32(byte[] buffer, ref int pos, uint value)
        {
            buffer[pos++] = (byte)value;
            buffer[pos++] = (byte)(value >> 8);
            buffer[pos++] = (byte)(value >> 16);
            buffer[pos++] = (byte)(value >> 24);
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
