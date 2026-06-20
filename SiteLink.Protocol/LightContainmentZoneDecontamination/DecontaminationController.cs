using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Mirror;
using UnityEngine;

namespace LightContainmentZoneDecontamination
{
	public class DecontaminationController : NetworkBehaviour
	{
		public enum DecontaminationStatus : byte
		{
			None,
			Disabled,
			Forced
		}
	}
}
