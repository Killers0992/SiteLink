using Mirror;
using UnityEngine;

namespace UserSettings.ServerSpecific
{
	public class SSKeybindSetting : ServerSpecificSettingBase
	{
		public bool SyncIsPressed { get; private set; }

		public bool PreventInteractionOnGUI { get; private set; }

		public bool AllowSpectatorTrigger { get; private set; }

		public KeyCode SuggestedKey { get; private set; }

		public SSKeybindSetting (int? id, string label, KeyCode suggestedKey = KeyCode.None, bool preventInteractionOnGui = true, bool allowSpectatorTrigger = true, string hint = null, byte collectionId = byte.MaxValue)
		{
			SetId (id, label);
			base.Label = label;
			base.CollectionId = collectionId;
			SuggestedKey = suggestedKey;
			PreventInteractionOnGUI = preventInteractionOnGui;
			AllowSpectatorTrigger = allowSpectatorTrigger;
			base.HintDescription = hint;
		}

		public override void ApplyDefaultValues ()
		{
			SyncIsPressed = false;
		}
	}
}
