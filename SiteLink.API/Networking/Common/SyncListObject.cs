using Mirror;
using Utils.Networking;

namespace SiteLink.API.Networking.Common;

public class SyncListObject<T> : SyncedNetworkProperty
{
    public uint Count = 0;

    public List<T> Items;

    public SyncListObject(uint count = 0) : base()
    {
        Count = count;
        Items = new List<T>((int)count);
    }

    public int Index;
    public byte Value;

    public uint Changes;

    public void Set(int index, byte val)
    {
        Index = index;
        Value = val;

        Changes = 1;
    }

    public override void OnSerializeAll(NetworkWriter writer)
    {
        writer.WriteUInt(Count);

        for (int i = 0; i < Count; i++)
        {
            T type = (T)Activator.CreateInstance(typeof(T));

            writer.Write(type);
        }

        writer.WriteUInt(0);
    }

    public override void OnSerializeDelta(NetworkWriter writer)
    {
        writer.WriteUInt(Changes);

        if (Changes > 0)
        {
            // OP_SET
            writer.WriteByte((byte)4);

            writer.WriteUInt((uint)Index);
            writer.WriteByte(Value);
        }

        Changes = 0;
    }

    public override void OnDeserializeAll(NetworkReader reader)
    {
        int count = (int)reader.ReadUInt();

        for (int i = 0; i < count; i++)
        {
            reader.Read<T>();
        }

        reader.ReadUInt();
    }

    public override void OnDeserializeDelta(NetworkReader reader)
    {
        int count = (int)reader.ReadUInt();
    }
}
