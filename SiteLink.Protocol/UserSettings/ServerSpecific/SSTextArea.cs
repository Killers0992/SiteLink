using System;
using Mirror;
using TMPro;

namespace UserSettings.ServerSpecific
{
	public class SSTextArea : ServerSpecificSettingBase
	{
		public enum FoldoutMode
		{
			NotCollapsable,
			CollapseOnEntry,
			ExtendOnEntry,
			CollapsedByDefault,
			ExtendedByDefault
		}

		public FoldoutMode Foldout { get; private set; }

		public TextAlignmentOptions AlignmentOptions { get; private set; }

		public SSTextArea (int? id, string content, FoldoutMode foldoutMode = FoldoutMode.NotCollapsable, string collapsedText = null, TextAlignmentOptions textAlignment = TextAlignmentOptions.TopLeft)
		{
			SetId (id, content);
			base.Label = content;
			base.HintDescription = collapsedText;
			Foldout = foldoutMode;
			AlignmentOptions = textAlignment;
		}

		public override void ApplyDefaultValues ()
		{
		}
	}
}
