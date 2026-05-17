using System;
using Mirror;
using TMPro;

namespace UserSettings.ServerSpecific
{
	public class SSPlaintextSetting : ServerSpecificSettingBase
	{
		public string SyncInputText { get; internal set; }

		public string Placeholder { get; private set; }

		public string DefaultText { get; private set; }

		public TMP_InputField.ContentType ContentType { get; private set; }

		public int CharacterLimit { get; private set; }

		public SSPlaintextSetting (int? id, string label, string placeholder = "...", int characterLimit = 64, TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard, string hint = null, byte collectionId = byte.MaxValue, bool isServerOnly = false)
		{
			SetId (id, label);
			base.Label = label;
			base.HintDescription = hint;
			base.CollectionId = collectionId;
			base.IsServerOnly = isServerOnly;
			Placeholder = placeholder;
			CharacterLimit = characterLimit;
			ContentType = contentType;
		}

		public override void ApplyDefaultValues ()
		{
			SyncInputText = (base.IsServerOnly ? DefaultText : string.Empty);
		}
	}
}
