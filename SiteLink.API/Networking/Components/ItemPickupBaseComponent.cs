using InventorySystem.Items.Pickups;
using System;

namespace SiteLink.API.Networking.Components;

public class ItemPickupBaseComponent : BehaviourComponent
{
    private PickupSyncInfo _info;

    public PickupSyncInfo Info
    {
        get => _info;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _info = value;
        }
    }

    public ItemPickupBaseComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public ItemPickupBaseComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WritePickupSyncInfo(_info);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WritePickupSyncInfo(_info);
        }
    }

}
