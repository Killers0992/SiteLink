using System;
using System.Text;
using Mirror;
using Utils.Networking;

namespace UserSettings.ServerSpecific
{
	public abstract class ServerSpecificSettingBase
	{
		public int SettingId { get; private set; }

		public string Label { get; protected set; }

		public string HintDescription { get; protected set; }

		public byte CollectionId { get; protected set; } = byte.MaxValue;

		public bool IsServerOnly { get; protected set; }

		public string PlayerPrefsKey { get; private set; }

		public virtual void SerializeEntry (NetworkWriter writer)
		{
			writer.WriteInt (SettingId);
			writer.WriteString (Label);
			writer.WriteString (HintDescription);
			writer.WriteByte (CollectionId);
			writer.WriteBool (IsServerOnly);
		}

		public abstract void ApplyDefaultValues ();

		public virtual void TransferValue (ServerSpecificSettingBase other)
		{
		}

		internal void SetId (int? id, string labelFallback)
		{
			if (!id.HasValue) {
				id = labelFallback.GetStableHashCode ();
			}
			SettingId = id.Value;
		}
	}
}
