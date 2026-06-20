using Mirror;

namespace RelativePositioning
{
	public static class RelativePositionSerialization
	{
		public static void WriteRelativePosition (this NetworkWriter writer, RelativePosition msg)
		{
			msg.Write (writer);
		}

		public static RelativePosition ReadRelativePosition (this NetworkReader reader)
		{
			return new RelativePosition (reader);
		}
	}
}
