using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class PlayerAuthenticationManagerComponent : BehaviourComponent
{
    private string _syncedUserId;

    public string SyncedUserId
    {
        get => _syncedUserId;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _syncedUserId = value;
        }
    }

    public PlayerAuthenticationManagerComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public PlayerAuthenticationManagerComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteString(_syncedUserId);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteString(_syncedUserId);
        }
    }

}
