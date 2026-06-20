using System;

[Flags]
public enum PlayerInfoArea
{
	Nickname = 1,
	Badge = 2,
	CustomInfo = 4,
	Role = 8,
	UnitName = 0x10,
	PowerStatus = 0x20
}
