using System;
using System.Diagnostics;
using Mirror;
using UnityEngine;

namespace UserSettings.ServerSpecific
{
	public class SSButton : ServerSpecificSettingBase
	{
		public readonly Stopwatch SyncLastPress = new Stopwatch ();

		public float HoldTimeSeconds { get; private set; }

		public string ButtonText { get; private set; }

		public SSButton (int? id, string label, string buttonText, float? holdTimeSeconds = null, string hint = null)
		{
			SetId (id, label);
			base.Label = label;
			base.HintDescription = hint;
			ButtonText = buttonText;
			HoldTimeSeconds = Mathf.Max (holdTimeSeconds.GetValueOrDefault (), 0f);
		}

		public override void ApplyDefaultValues ()
		{
			SyncLastPress.Reset ();
		}
	}
}
