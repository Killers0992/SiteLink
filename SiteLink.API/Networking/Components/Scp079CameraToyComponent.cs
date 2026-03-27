using Mirror;
using System;
using AdminToys;
using MapGeneration;
using UnityEngine;

namespace SiteLink.API.Networking.Components;

public class Scp079CameraToyComponent : AdminToyBaseComponent
{
    private string _label;

    private RoomIdentifier _room;

    private Vector2 _verticalConstraint;

    private Vector2 _horizontalConstraint;

    private Vector2 _zoomConstraint;

    public string Label
    {
        get => _label;
        set
        {
            SetSyncVarDirtyBit(32UL);
            _label = value;
        }
    }

    public RoomIdentifier Room
    {
        get => _room;
        set
        {
            SetSyncVarDirtyBit(64UL);
            _room = value;
        }
    }

    public Vector2 VerticalConstraint
    {
        get => _verticalConstraint;
        set
        {
            SetSyncVarDirtyBit(128UL);
            _verticalConstraint = value;
        }
    }

    public Vector2 HorizontalConstraint
    {
        get => _horizontalConstraint;
        set
        {
            SetSyncVarDirtyBit(256UL);
            _horizontalConstraint = value;
        }
    }

    public Vector2 ZoomConstraint
    {
        get => _zoomConstraint;
        set
        {
            SetSyncVarDirtyBit(512UL);
            _zoomConstraint = value;
        }
    }

    public Scp079CameraToyComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteString(_label);
            writer.WriteRoomIdentifier(_room);
            writer.WriteVector2(_verticalConstraint);
            writer.WriteVector2(_horizontalConstraint);
            writer.WriteVector2(_zoomConstraint);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 32UL) != 0UL)
        {
            writer.WriteString(_label);
        }

        if ((SyncVarDirtyBits & 64UL) != 0UL)
        {
            writer.WriteRoomIdentifier(_room);
        }

        if ((SyncVarDirtyBits & 128UL) != 0UL)
        {
            writer.WriteVector2(_verticalConstraint);
        }

        if ((SyncVarDirtyBits & 256UL) != 0UL)
        {
            writer.WriteVector2(_horizontalConstraint);
        }

        if ((SyncVarDirtyBits & 512UL) != 0UL)
        {
            writer.WriteVector2(_zoomConstraint);
        }
    }

}
