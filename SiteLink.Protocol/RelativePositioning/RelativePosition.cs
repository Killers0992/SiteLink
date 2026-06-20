using System;
using Mirror;
using UnityEngine;

namespace RelativePositioning
{
	public struct RelativePosition : NetworkMessage, IEquatable<RelativePosition>
    {
        private static readonly float InverseAccuracy = 0.00390625f;

        public readonly short PositionX;

		public readonly short PositionY;

		public readonly short PositionZ;

		public readonly byte WaypointId;

        public bool OutOfRange;

        public Vector3 Relative
        {
            get
            {
                return new Vector3(PositionX * RelativePosition.InverseAccuracy, PositionY * RelativePosition.InverseAccuracy, PositionZ * RelativePosition.InverseAccuracy);
            }
        }

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

			OutOfRange = false;
		}

        public RelativePosition(byte waypoint, short x, short y, short z, bool outOfRange)
        {
            WaypointId = waypoint;

            PositionX = x;
            PositionY = y;
            PositionZ = z;

            OutOfRange = outOfRange;
        }

        public void Write (NetworkWriter writer)
		{
			writer.WriteByte (WaypointId);

			if (WaypointId > 0) 
			{
				writer.WriteShort (PositionX);
				writer.WriteShort (PositionY);
				writer.WriteShort (PositionZ);
			}
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
