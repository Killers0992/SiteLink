using System;
using Mirror;
using UnityEngine;

namespace UserSettings.ServerSpecific
{
	public class SSDropdownSetting : ServerSpecificSettingBase
	{
		public enum DropdownEntryType
		{
			Regular,
			Scrollable,
			ScrollableLoop,
			Hybrid,
			HybridLoop
		}

		public string[] Options { get; private set; }

		public int DefaultOptionIndex { get; private set; }

		public DropdownEntryType EntryType { get; private set; }

		public int SyncSelectionIndexRaw { get; internal set; }

		public SSDropdownSetting (int? id, string label, string[] options, int defaultOptionIndex = 0, DropdownEntryType entryType = DropdownEntryType.Regular, string hint = null, byte collectionId = byte.MaxValue, bool isServerOnly = false)
		{
			SetId (id, label);
			if (options == null || options.Length == 0) {
				options = new string[0];
			}
			base.Label = label;
			base.HintDescription = hint;
			base.CollectionId = collectionId;
			base.IsServerOnly = isServerOnly;
			Options = options;
			EntryType = entryType;
			DefaultOptionIndex = defaultOptionIndex;
		}

		public override void SerializeEntry (NetworkWriter writer)
		{
			base.SerializeEntry (writer);
			writer.WriteByte ((byte)DefaultOptionIndex);
			writer.WriteByte ((byte)EntryType);
			writer.WriteByte ((byte)Options.Length);
			foreach (string option in Options) {
				writer.WriteString (option);
			}
		}

		public override void ApplyDefaultValues ()
		{
			SyncSelectionIndexRaw = DefaultOptionIndex;
		}
	}
}
