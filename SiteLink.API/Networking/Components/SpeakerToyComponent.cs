using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class SpeakerToyComponent : AdminToyBaseComponent
{
    private byte _controllerId;

    private bool _isSpatial;

    private float _volume;

    private float _minDistance;

    private float _maxDistance;

    public byte ControllerId
    {
        get => _controllerId;
        set
        {
            SetSyncVarDirtyBit(32UL);
            _controllerId = value;
        }
    }

    public bool IsSpatial
    {
        get => _isSpatial;
        set
        {
            SetSyncVarDirtyBit(64UL);
            _isSpatial = value;
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            SetSyncVarDirtyBit(128UL);
            _volume = value;
        }
    }

    public float MinDistance
    {
        get => _minDistance;
        set
        {
            SetSyncVarDirtyBit(256UL);
            _minDistance = value;
        }
    }

    public float MaxDistance
    {
        get => _maxDistance;
        set
        {
            SetSyncVarDirtyBit(512UL);
            _maxDistance = value;
        }
    }

    public SpeakerToyComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteByte(_controllerId);
            writer.WriteBool(_isSpatial);
            writer.WriteFloat(_volume);
            writer.WriteFloat(_minDistance);
            writer.WriteFloat(_maxDistance);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 32UL) != 0UL)
        {
            writer.WriteByte(_controllerId);
        }

        if ((SyncVarDirtyBits & 64UL) != 0UL)
        {
            writer.WriteBool(_isSpatial);
        }

        if ((SyncVarDirtyBits & 128UL) != 0UL)
        {
            writer.WriteFloat(_volume);
        }

        if ((SyncVarDirtyBits & 256UL) != 0UL)
        {
            writer.WriteFloat(_minDistance);
        }

        if ((SyncVarDirtyBits & 512UL) != 0UL)
        {
            writer.WriteFloat(_maxDistance);
        }
    }

}
