using Mirror;
using UnityEngine;
using AdminToys;

namespace SiteLink.API.Networking.Components;

public class PrimitiveObjectToyComponent : AdminToyBaseComponent
{
    private PrimitiveType _primitiveType;

    private Color _materialColor;

    private PrimitiveFlags _primitiveFlags;

    public PrimitiveType PrimitiveType
    {
        get => _primitiveType;
        set
        {
            SetSyncVarDirtyBit(32UL);
            _primitiveType = value;
        }
    }

    public Color MaterialColor
    {
        get => _materialColor;
        set
        {
            SetSyncVarDirtyBit(64UL);
            _materialColor = value;
        }
    }

    public PrimitiveFlags PrimitiveFlags
    {
        get => _primitiveFlags;
        set
        {
            SetSyncVarDirtyBit(128UL);
            _primitiveFlags = value;
        }
    }

    public PrimitiveObjectToyComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.Write(_primitiveType);
            writer.WriteColor(_materialColor);
            writer.Write(_primitiveFlags);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 32UL) != 0UL)
        {
            writer.Write(_primitiveType);
        }

        if ((SyncVarDirtyBits & 64UL) != 0UL)
        {
            writer.WriteColor(_materialColor);
        }

        if ((SyncVarDirtyBits & 128UL) != 0UL)
        {
            writer.Write(_primitiveFlags);
        }
    }

}
