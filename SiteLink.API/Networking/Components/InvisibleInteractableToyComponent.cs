using Mirror;
using AdminToys;
using static AdminToys.InvisibleInteractableToy;
using System;

namespace SiteLink.API.Networking.Components;

public class InvisibleInteractableToyComponent : AdminToyBaseComponent
{
    private ColliderShape _shape;

    private float _interactionDuration;

    private bool _isLocked;

    public ColliderShape Shape
    {
        get => _shape;
        set
        {
            SetSyncVarDirtyBit(32UL);
            _shape = value;
        }
    }

    public float InteractionDuration
    {
        get => _interactionDuration;
        set
        {
            SetSyncVarDirtyBit(64UL);
            _interactionDuration = value;
        }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            SetSyncVarDirtyBit(128UL);
            _isLocked = value;
        }
    }

    public InvisibleInteractableToyComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.Write(_shape);
            writer.WriteFloat(_interactionDuration);
            writer.WriteBool(_isLocked);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 32UL) != 0UL)
        {
            writer.Write(_shape);
        }

        if ((SyncVarDirtyBits & 64UL) != 0UL)
        {
            writer.WriteFloat(_interactionDuration);
        }

        if ((SyncVarDirtyBits & 128UL) != 0UL)
        {
            writer.WriteBool(_isLocked);
        }
    }

}
