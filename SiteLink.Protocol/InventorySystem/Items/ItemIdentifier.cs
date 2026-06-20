using System;

namespace InventorySystem.Items
{
	[Serializable]
	public readonly struct ItemIdentifier : IEquatable<ItemIdentifier>
	{
		public static ItemIdentifier None = new ItemIdentifier (ItemType.None, 0);

		public readonly ItemType TypeId;

		public readonly ushort SerialNumber;

		public ItemIdentifier (ItemType t, ushort s)
		{
			TypeId = t;
			SerialNumber = s;
		}

		public ItemIdentifier (ItemBase item)
		{
			TypeId = item.ItemTypeId;
			SerialNumber = item.ItemSerial;
		}

		public override int GetHashCode ()
		{
			return SerialNumber;
		}

		public static bool operator == (ItemIdentifier left, ItemIdentifier right)
		{
			return left.Equals (right);
		}

		public static bool operator != (ItemIdentifier left, ItemIdentifier right)
		{
			return !left.Equals (right);
		}

		public bool Equals (ItemIdentifier other)
		{
			if (SerialNumber == other.SerialNumber) {
				return TypeId == other.TypeId;
			}
			return false;
		}

		public override bool Equals (object obj)
		{
			if (obj is ItemIdentifier other) {
				return Equals (other);
			}
			return false;
		}

		public override string ToString ()
		{
			return string.Format ("{0} (type={1} serial={2})", "ItemIdentifier", TypeId, SerialNumber);
		}
	}
}
