using System;
using System.Collections.Generic;
using UnityEngine;
using Utils.Networking;

namespace UserSettings.ServerSpecific
{
	public static class ServerSpecificSettingsSync
	{
		private static Type[] _allTypes;

		private static readonly Func<ServerSpecificSettingBase>[] AllSettingConstructors = new Func<ServerSpecificSettingBase>[8] {
			() => new SSGroupHeader (0, null),
			() => new SSKeybindSetting (0, null),
			() => new SSDropdownSetting (0, null, null),
			() => new SSTwoButtonsSetting (0, null, null, null),
			() => new SSSliderSetting (0, null, 0f, 0f),
			() => new SSPlaintextSetting (0, null),
			() => new SSButton (0, null, null),
			() => new SSTextArea (0, null)
		};

		public static ServerSpecificSettingBase[] DefinedSettings { get; set; }

		private static Type[] AllSettingTypes {
			get {
				if (_allTypes != null) {
					return _allTypes;
				}
				_allTypes = new Type[AllSettingConstructors.Length];
				for (int i = 0; i < _allTypes.Length; i++) {
					_allTypes [i] = AllSettingConstructors [i] ().GetType ();
				}
				return _allTypes;
			}
		}

		public static byte GetCodeFromType (Type type)
		{
			int num = AllSettingTypes.IndexOf (type);
			if (num < 0) {
				throw new ArgumentException (type.FullName + " is not a supported server-specific setting serializer.", "type");
			}
			return (byte)num;
		}

		public static Type GetTypeFromCode (byte header)
		{
			return AllSettingTypes [header];
		}

		public static ServerSpecificSettingBase CreateInstance (Type t)
		{
			return AllSettingConstructors [AllSettingTypes.IndexOf (t)] ();
		}
	}
}
