using System;

[Flags]
public enum CentralAuthPreauthFlags : byte
{
	None = 0,
	ReservedSlot = 1,
	IgnoreBans = 2,
	IgnoreWhitelist = 4,
	IgnoreGeoblock = 8,
	GloballyBanned = 0x10,
	NorthwoodStaff = 0x20,
	AuthRejected = 0x40
}
