namespace SiteLink.API.Networking.Common;

public class NetworkObject : IDisposable
{
    public virtual uint NetworkId { get; set; }
    public virtual uint AssetId { get; }
    public virtual ulong SceneId { get; }

    public bool WithPayload { get; set; }

    public Vector3 Position { get; set; } = Vector3.zero;
    public Quaternion Rotation { get; set; } = Quaternion.identity;
    public Vector3 Scale { get; set; } = Vector3.one;

    public World World { get; private set; }
    public Session Owner { get; private set; }

    public BehaviourComponent[] Behaviours { get; set; }

    public List<Session> Observers = new List<Session>();

    public NetworkObject(World world, Session owner, uint networkId = 0)
    {
        Owner = owner;
        World = world;

        if (networkId != 0)
            NetworkId = networkId;

        if (NetworkId == 0)
        {
            NetworkId = world.GetFreeId();
            world.Objects.Add(NetworkId, this);
        }
        else
        {
            NetworkId = networkId;

            if (world != null && !world.Objects.ContainsKey(networkId))
                world.Objects.Add(networkId, this);
        }
    }

    public void MoveToWorld(World world)
    {
        if (world == null)
        {
            return;
        }

        World.Objects.Remove(NetworkId);

        world.Objects.Add(NetworkId, this);
        World = world;
    }

    public void SendUpdate(Session client)
    {
        if (!Observers.Contains(client))
        {
            SpawnWithPayload(client);
            return;
        }

        NetworkWriter wr2 = Serialize(false);

        if (wr2 == null)
            return;

        NetworkWriter wr = new NetworkWriter();
        wr.WriteUShort(NetworkMessageId<EntityStateMessage>.Id);
        wr.WriteUInt(NetworkId);
        wr.WriteArraySegmentAndSize(wr2.ToArraySegment());

        client.Connection.AsServer.Send(wr);
    }

    public void Deserialize(NetworkReader reader, bool intialState)
    {
        ulong mask = Compression.DecompressVarUInt(reader);

        for (int i = 0; i < Behaviours.Length; ++i)
        {
            if (IsDirty(mask, i))
            {
                BehaviourComponent comp = Behaviours[i];

                comp.Deserialize(reader, intialState);
            }
        }
    }

    public NetworkWriter Serialize(bool intialState)
    {
        NetworkWriter writer = new NetworkWriter();

        ulong observerMask = DirtyMasks(intialState);

        if (observerMask != 0)
            Compression.CompressVarUInt(writer, observerMask);

        if (observerMask != 0)
        {
            for (int x = 0; x < Behaviours.Length; x++)
            {
                BehaviourComponent bInfo = Behaviours[x];

                if (bInfo == null)
                    continue;

                bool observersDirty = IsDirty(observerMask, x);

                if (observersDirty)
                {
                    NetworkWriter temp = new NetworkWriter();

                    bInfo.Serialize(temp, intialState);

                    ArraySegment<byte> segment = temp.ToArraySegment();

                    writer.WriteBytes(segment.Array, segment.Offset, segment.Count);

                    if (!intialState)
                        bInfo.ClearAllDirtyBits();
                }
            }
        }
        else
            return null;

        return writer;
    }

    internal static bool IsDirty(ulong mask, int index)
    {
        ulong nthBit = (ulong)(1 << index);
        return (mask & nthBit) != 0;
    }

    public ulong DirtyMasks(bool intialState)
    {
        ulong bit = 0;

        for (int x = 0; x < Behaviours.Length; x++)
        {
            BehaviourComponent bInfo = Behaviours[x];

            if (bInfo == null)
                continue;

            bool isDirty = bInfo.IsDirty();

            ulong behaviorBit = 1UL << (x & 31);

            if (intialState || isDirty)
            {
                bit |= behaviorBit;
            }
        }

        return bit;
    }

    public void AssignOwner(Session owner)
    {
        Owner = owner;
    }

    public void Destroy(Session client)
    {
        if (client.Connection == null)
        {
            SiteLinkLogger.Error($"Failed to destroy {NetworkId} for client {client.UserId} because connection is null!", "NetworkObject");
            return;
        }

        client.Connection.AsServer.Destroy(NetworkId);
    }

    public void Destroy()
    {
        if (Owner != null)
            Owner.Connection.AsServer.Destroy(NetworkId);

        Dispose();
    }

    public void SpawnWithPayload(Session client)
    {
        NetworkWriter wr2 = Serialize(true);
        Spawn(client, wr2.ToArraySegment());
    }

    public void Spawn(Session client, ArraySegment<byte> payload = default)
    {
        client.Connection.AsServer.Spawn(
            NetworkId,
            Owner == client,
            Owner == client,
            SceneId,
            AssetId,
            Position,
            Rotation,
            Scale,
            payload);

        Observers.Add(client);
    }

    public virtual bool OnReceiveCommand(byte componentIndex, ushort functionHash, NetworkReader reader)
    {
        bool runCommand = true;

        if (Behaviours.Length > componentIndex && Behaviours[componentIndex] != null)
            runCommand = Behaviours[componentIndex].OnReceiveCommand(functionHash, reader);

        return runCommand;
    }

    public void Dispose()
    {
        World.Objects.Remove(NetworkId);
        Owner = null;
    }
}