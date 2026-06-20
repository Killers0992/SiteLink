using System;
using Mirror;
using UnityEngine;

namespace UserSettings.ServerSpecific
{
	public class SSSliderSetting : ServerSpecificSettingBase
	{
		public float SyncFloatValue { get; set; }

		public float DefaultValue { get; private set; }

		public float MinValue { get; private set; }

		public float MaxValue { get; private set; }

		public bool Integer { get; private set; }

		public string ValueToStringFormat { get; private set; }

		public string FinalDisplayFormat { get; private set; }

		public SSSliderSetting (int? id, string label, float minValue, float maxValue, float defaultValue = 0f, bool integer = false, string valueToStringFormat = "0.##", string finalDisplayFormat = "{0}", string hint = null, byte collectionId = byte.MaxValue, bool isServerOnly = false)
		{
			SetId (id, label);
			base.Label = label;
			base.HintDescription = hint;
			base.CollectionId = collectionId;
			base.IsServerOnly = isServerOnly;
			DefaultValue = Mathf.Clamp (defaultValue, minValue, maxValue);
			MinValue = minValue;
			MaxValue = maxValue;
			Integer = integer;
			ValueToStringFormat = valueToStringFormat;
			FinalDisplayFormat = finalDisplayFormat;
			if (!finalDisplayFormat.Contains ("0")) {
				FinalDisplayFormat += "{0}";
			}
		}

		public override void ApplyDefaultValues ()
		{
			SyncFloatValue = DefaultValue;
		}
	}
}
