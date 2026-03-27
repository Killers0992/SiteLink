using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class QueryProcessorComponent : BehaviourComponent
{
    private bool _overridePasswordEnabled;

    public bool OverridePasswordEnabled
    {
        get => _overridePasswordEnabled;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _overridePasswordEnabled = value;
        }
    }

    public QueryProcessorComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public QueryProcessorComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteBool(_overridePasswordEnabled);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteBool(_overridePasswordEnabled);
        }
    }

}
