using System;
using Mirror;

namespace UserSettings.ServerSpecific
{
	/// <summary>
	/// Client -> server answer to a server-specific setting. Mirrors the game's own struct;
	/// the payload is kept raw because the proxy only needs the id to decide whether the
	/// response is one of its own entries.
	/// </summary>
	public readonly struct SSSClientResponse
	{
		public readonly Type SettingType;

		public readonly int Id;

		public readonly ArraySegment<byte> Payload;

		public SSSClientResponse (NetworkReader reader)
		{
			SettingType = ServerSpecificSettingsSync.GetTypeFromCode (reader.ReadByte ());
			Id = reader.ReadInt ();
			Payload = reader.ReadBytesSegment (reader.ReadInt ());
		}
	}
}
