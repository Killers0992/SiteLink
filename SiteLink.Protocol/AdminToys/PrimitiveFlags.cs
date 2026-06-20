using System;

namespace AdminToys
{
	[Flags]
	public enum PrimitiveFlags : byte
	{
		None = 0,
		Collidable = 1,
		Visible = 2
	}
}
