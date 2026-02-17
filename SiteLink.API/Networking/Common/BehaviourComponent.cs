namespace SiteLink.API.Networking.Common;

public class BehaviourComponent
{
    public ulong SyncVarDirtyBits = 0;
    public ulong SyncObjectsDirtyBits = 0;

    public NetworkObject Object { get; }

    public SyncedNetworkProperty[] SyncObjects { get; }


    public Action<NetworkWriter, bool> OnBeforeSerialize;
    public Action<NetworkWriter, bool> OnAfterSerialize;

    public Action<NetworkReader, long, bool> OnDeserializeSyncVars;

    public BehaviourComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects)
    {
        Object = networkObject;
        SyncObjects = objects;
    }

    public virtual bool OnReceiveCommand(ushort functionHash, NetworkReader reader) => true;

    public void SetSyncVarDirtyBit(ulong dirtyBit)
    {
        SyncVarDirtyBits |= dirtyBit;
    }

    public void SetSyncObjectDirtyBit(ulong dirtyBit)
    {
        SyncObjectsDirtyBits |= dirtyBit;
    }

    public void ClearAllDirtyBits()
    {
        SyncVarDirtyBits = 0;
        SyncObjectsDirtyBits = 0;
    }

    public bool IsDirty() => (SyncVarDirtyBits | SyncObjectsDirtyBits) != 0;

    public void Serialize(NetworkWriter writer, bool initialState)
    {
        int headerPosition = writer.Position;
        writer.WriteByte(0);

        int contentPosition = writer.Position;
        try
        {
            OnSerialize(writer, initialState);
        }
        catch (Exception e)
        {
            SiteLinkLogger.Error($"OnSerialize failed\n\n{e}");
        }

        int endPosition = writer.Position;
        writer.Position = headerPosition;

        int size = endPosition - contentPosition;
        byte safety = (byte)(size & 0xFF);

        writer.WriteByte(safety);
        writer.Position = endPosition;
    }

    public bool Deserialize(NetworkReader reader, bool initialState)
    {
        bool result = true;

        byte safety = reader.ReadByte();
        int chunkStart = reader.Position;

        try
        {
            OnDeserialize(reader, initialState);
        }
        catch (Exception e)
        {
            SiteLinkLogger.Error(e);
            result = false;
        }

        int size = reader.Position - chunkStart;
        byte sizeHash = (byte)(size & 0xFF);
        if (sizeHash != safety)
        {
            int correctedSize = ErrorCorrection(size, safety);
            reader.Position = chunkStart + correctedSize;
            result = false;
        }

        return result;
    }

    void OnDeserialize(NetworkReader reader, bool initialState)
    {
        DeserializeSyncObjects(reader, initialState);
        DeserializeSyncVars(reader, initialState);
    }

    void SerializeSyncObjects(NetworkWriter writer, bool initialState)
    {
        if (initialState)
            SerializeObjectsAll(writer);
        else
            SerializeObjectsDelta(writer);
    }

    public virtual void OnSerialize(NetworkWriter writer, bool initialState)
    {
        OnBeforeSerialize?.Invoke(writer, initialState);

        SerializeSyncObjects(writer, initialState);
        SerializeSyncVars(writer, initialState);

        OnAfterSerialize?.Invoke(writer, initialState);
    }

    protected virtual void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {

    }

    void SerializeObjectsAll(NetworkWriter writer)
    {
        for (int i = 0; i < SyncObjects.Length; i++)
        {
            SyncObjects[i].OnSerializeAll(writer);
        }
    }

    void SerializeObjectsDelta(NetworkWriter writer)
    {
        writer.WriteULong(SyncObjectsDirtyBits);
        for (int i = 0; i < SyncObjects.Length; i++)
        {
            SyncedNetworkProperty syncObject = SyncObjects[i];

            if ((SyncObjectsDirtyBits & 1UL << i) != 0UL)
                syncObject.OnSerializeDelta(writer);
        }
    }

    void DeserializeSyncObjects(NetworkReader reader, bool initialState)
    {
        if (initialState)
            DeserializeObjectsAll(reader);
        else
            DeserializeObjectsDelta(reader);
    }

    void DeserializeSyncVars(NetworkReader reader, bool initialState)
    {
        long mask = 0;

        if (!initialState)
            mask = (long)reader.ReadULong();

        OnDeserializeSyncVars?.Invoke(reader, mask, initialState);
    }

    void DeserializeObjectsAll(NetworkReader reader)
    {
        for (int i = 0; i < SyncObjects.Length; i++)
        {
            SyncedNetworkProperty syncObject = SyncObjects[i];
            syncObject.OnDeserializeAll(reader);
        }
    }

    void DeserializeObjectsDelta(NetworkReader reader)
    {
        ulong dirty = reader.ReadULong();
        for (int i = 0; i < SyncObjects.Length; i++)
        {
            SyncedNetworkProperty syncObject = SyncObjects[i];
            if ((dirty & 1UL << i) != 0)
                syncObject.OnDeserializeDelta(reader);
        }
    }

    internal static int ErrorCorrection(int size, byte safety)
    {
        uint cleared = (uint)size & 0xFFFFFF00;

        return (int)(cleared | safety);
    }
}