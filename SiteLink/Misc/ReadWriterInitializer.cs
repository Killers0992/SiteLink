using InventorySystem.Items;
using Mirror;

namespace SiteLink.Misc;

public static class ReadWriterInitializer
{
    public static void InitializeAll()
    {
        Writer<byte>.write = WriteByte;

        Reader<ItemIdentifier>.read = ReadItemIdentifier;
        Writer<ItemIdentifier>.write = WriteItemIdentifier;

        Writer<PlayerInfoArea>.write = WritePlayerInfoArea;
        Reader<PlayerInfoArea>.read = ReadPlayerInfoArea;
    }

    // byte

    public static void WriteByte(NetworkWriter writer, byte b) => writer.WriteByte(b);

    // ItemIdentifier

    public static void WriteItemIdentifier(NetworkWriter writer, ItemIdentifier itemIdentifier)
    {
        writer.WriteItemType(itemIdentifier.TypeId);
        writer.WriteUShort(itemIdentifier.SerialNumber);
    }

    public static ItemIdentifier ReadItemIdentifier(NetworkReader reader) => new ItemIdentifier(reader.ReadItemType(), reader.ReadUShort());

    // ItemType
    public static void WriteItemType(this NetworkWriter writer, ItemType itemType) => writer.WriteInt((int)itemType);
    public static ItemType ReadItemType(this NetworkReader reader) => (ItemType) reader.ReadInt();

    // PlayerInfoArea
    public static void WritePlayerInfoArea(this NetworkWriter writer, PlayerInfoArea area) => writer.WriteInt((int)area);
    public static PlayerInfoArea ReadPlayerInfoArea(this NetworkReader reader) => (PlayerInfoArea) reader.ReadInt();
}
