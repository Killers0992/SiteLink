using Mirror;

namespace UserSettings.ServerSpecific
{
	public class SSGroupHeader : ServerSpecificSettingBase
	{
		public bool ReducedPadding { get; private set; }

		public SSGroupHeader (string label, bool reducedPadding = false, string hint = null)
			: this (null, label, reducedPadding, hint)
		{
		}

		public SSGroupHeader (int? id, string label, bool reducedPadding = false, string hint = null)
		{
			SetId (id, label);
			base.Label = label;
			base.HintDescription = hint;
			ReducedPadding = reducedPadding;
		}

		public override void ApplyDefaultValues ()
		{
		}
	}
}
