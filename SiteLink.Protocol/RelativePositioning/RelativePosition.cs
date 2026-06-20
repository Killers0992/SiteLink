using System;
using Mirror;

namespace RelativePositioning
{
	public struct RelativePosition : NetworkMessage, IEquatable<RelativePosition>
	{
		public readonly short PositionX;

		public readonly short PositionY;

		public readonly short PositionZ;

		public readonly byte WaypointId;

		public RelativePosition (NetworkReader reader)
		{
			WaypointId = reader.ReadByte ();
			if (WaypointId > 0) {
				PositionX = reader.ReadShort ();
				PositionY = reader.ReadShort ();
				PositionZ = reader.ReadShort ();
			} else {
				PositionX = 0;
				PositionY = 0;
				PositionZ = 0;
			}
			//OutOfRange = false;
		}

		public void Write (NetworkWriter writer)
		{
			writer.WriteByte (WaypointId);
			if (WaypointId > 0) {
				writer.WriteShort (PositionX);
				writer.WriteShort (PositionY);
				writer.WriteShort (PositionZ);
			}
		}

		private static bool TryCompressPosition (float pos, out short compressed)
		{
			float num = pos * 256f;
			if (num < -32768f) {
				compressed = short.MinValue;
				return false;
			}
			if (num > 32767f) {
				compressed = short.MaxValue;
				return false;
			}
			compressed = (short)num;
			return true;
		}

		public bool Equals (RelativePosition other)
		{
			if (PositionX == other.PositionX && PositionY == other.PositionY && PositionZ == other.PositionZ) {
				return WaypointId == other.WaypointId;
			}
			return false;
		}

		public override bool Equals (object obj)
		{
			if (obj is RelativePosition relativePosition) {
				return relativePosition.Equals (this);
			}
			return false;
		}

		public override int GetHashCode ()
		{
			return (((((WaypointId * 397) ^ PositionX) * 397) ^ PositionZ) * 397) ^ PositionY;
		}
	}
}
