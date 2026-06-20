using UnityEngine;

namespace InventorySystem.Items
{
	public abstract class ItemBase : MonoBehaviour
	{
		public ItemType ItemTypeId;

		public ushort ItemSerial { get; internal set; }
	}
}
