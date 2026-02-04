using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class CharacterClassManagerComponent : BehaviourComponent
{
    private string _pastebin;

    private ushort _maxPlayers;

    private bool _roundStarted;

    public string Pastebin
    {
        get => _pastebin;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _pastebin = value;
        }
    }

    public ushort MaxPlayers
    {
        get => _maxPlayers;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _maxPlayers = value;
        }
    }

    public bool RoundStarted
    {
        get => _roundStarted;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _roundStarted = value;
        }
    }

    public CharacterClassManagerComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public CharacterClassManagerComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteString(_pastebin);
            writer.WriteUShort(_maxPlayers);
            writer.WriteBool(_roundStarted);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteString(_pastebin);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteUShort(_maxPlayers);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteBool(_roundStarted);
        }
    }

}
