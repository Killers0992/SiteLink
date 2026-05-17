using System;
using Mirror;

public static class RecyclablePlayerIdReaderWriter
{
	public static void WriteRecyclablePlayerId (this NetworkWriter writer, RecyclablePlayerId val)
	{
		for (int num = val.Value; num >= 0; num -= 255) {
			writer.WriteByte ((byte)Math.Min (num, 255));
		}
	}

	public static RecyclablePlayerId ReadRecyclablePlayerId (this NetworkReader reader)
	{
		int num = 0;
		byte b;
		do {
			b = reader.ReadByte ();
			num += b;
		} while (b == byte.MaxValue);
		return new RecyclablePlayerId (num);
	}
}
