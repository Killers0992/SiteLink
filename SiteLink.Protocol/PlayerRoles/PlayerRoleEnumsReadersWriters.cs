using Mirror;

namespace PlayerRoles
{
	public static class PlayerRoleEnumsReadersWriters
	{
		public static void WriteRoleType (this NetworkWriter writer, RoleTypeId role)
		{
			writer.WriteSByte ((sbyte)role);
		}

		public static RoleTypeId ReadRoleType (this NetworkReader reader)
		{
			return (RoleTypeId)reader.ReadSByte ();
		}
	}
}
