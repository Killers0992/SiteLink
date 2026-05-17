using System;

namespace PlayerStatsSystem
{
	[Flags]
	public enum AdminFlags
	{
		None = 0,
		Noclip = 1,
		GodMode = 2,
		BypassMode = 4
	}
}
