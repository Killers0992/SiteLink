using Mirror;
using InventorySystem.Items;
using System;

namespace SiteLink.API.Networking.Components;

public class InventoryComponent : BehaviourComponent
{
    private ItemIdentifier _curItem;

    private float _syncStaminaModifier;

    private float _syncMovementLimiter;

    private float _syncMovementMultiplier;

    public ItemIdentifier CurItem
    {
        get => _curItem;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _curItem = value;
        }
    }

    public float SyncStaminaModifier
    {
        get => _syncStaminaModifier;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _syncStaminaModifier = value;
        }
    }

    public float SyncMovementLimiter
    {
        get => _syncMovementLimiter;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _syncMovementLimiter = value;
        }
    }

    public float SyncMovementMultiplier
    {
        get => _syncMovementMultiplier;
        set
        {
            SetSyncVarDirtyBit(8UL);
            _syncMovementMultiplier = value;
        }
    }

    public InventoryComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public InventoryComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.Write(_curItem);
            writer.WriteFloat(_syncStaminaModifier);
            writer.WriteFloat(_syncMovementLimiter);
            writer.WriteFloat(_syncMovementMultiplier);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.Write(_curItem);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteFloat(_syncStaminaModifier);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteFloat(_syncMovementLimiter);
        }

        if ((SyncVarDirtyBits & 8UL) != 0UL)
        {
            writer.WriteFloat(_syncMovementMultiplier);
        }
    }

}
