using System;
using Mirror;

namespace UserSettings.ServerSpecific
{
	public class SSTwoButtonsSetting : ServerSpecificSettingBase
	{
		public bool SyncIsB { get; internal set; }

		public string OptionA { get; private set; }

		public string OptionB { get; private set; }

		public bool DefaultIsB { get; private set; }

		public SSTwoButtonsSetting (int? id, string label, string optionA, string optionB, bool defaultIsB = false, string hint = null, byte collectionId = byte.MaxValue, bool isServerOnly = false)
		{
			SetId (id, label);
			base.Label = label;
			base.CollectionId = collectionId;
			base.IsServerOnly = isServerOnly;
			OptionA = optionA;
			OptionB = optionB;
			DefaultIsB = defaultIsB;
			base.HintDescription = hint;
		}

		public override void ApplyDefaultValues ()
		{
			SyncIsB = DefaultIsB;
		}
	}
}
