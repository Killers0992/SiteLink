using SiteLink.API.Networking.Connections;
using SiteLink.Networking.Batchers;

namespace SiteLink.API.Misc
{
    public sealed class MirrorSender
    {
        private readonly CustomBatcher _batcher;
        private readonly Func<double> _timeSeconds;
        private readonly Action<byte[], int, int, DeliveryMethod> _sendRaw;

        // Reuse one writer for fetching batches (same pattern you already use)
        private readonly NetworkWriter _batchWriter = new NetworkWriter();

        public RemoteConnection Connection;

        public MirrorSender(
            RemoteConnection connection,
            int thresholdBytes,
            Func<double> timeSeconds,
            Action<byte[], int, int, DeliveryMethod> sendRaw)
        {
            Connection = connection;
            _batcher = new CustomBatcher(thresholdBytes);
            _timeSeconds = timeSeconds;
            _sendRaw = sendRaw;
        }

        /// <summary>
        /// Enqueue a ready Mirror packet (the writer already contains message id + payload).
        /// </summary>
        public void Send(NetworkWriter writer)
        {
            if (writer == null) return;
            _batcher.AddMessage(writer.ToArraySegment(), _timeSeconds());
            // don't null it here; caller owns it. (If you want, use a writer pool later.)
        }

        /// <summary>
        /// Convenience: build a message from an action.
        /// </summary>
        public void Send(Action<NetworkWriter> build)
        {
            var w = new NetworkWriter();
            build(w);
            Send(w);
        }

        /// <summary>
        /// Flush queued batches.
        /// Call once per tick (e.g., Client.PollEvents()).
        /// </summary>
        public void Update(DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            while (_batcher.GetBatch(_batchWriter))
            {
                ArraySegment<byte> seg = _batchWriter.ToArraySegment();
                _sendRaw(seg.Array!, seg.Offset, seg.Count, method);
                _batchWriter.Position = 0;
            }
        }
    }
}
