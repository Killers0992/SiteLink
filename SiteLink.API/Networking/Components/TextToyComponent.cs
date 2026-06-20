using Mirror;
using UnityEngine;
using System;

namespace SiteLink.API.Networking.Components;

public class TextToyComponent : AdminToyBaseComponent
{
    private Vector2 _displaySize;

    private string _textFormat;

    public Vector2 DisplaySize
    {
        get => _displaySize;
        set
        {
            SetSyncVarDirtyBit(32UL);
            _displaySize = value;
        }
    }

    public string TextFormat
    {
        get => _textFormat;
        set
        {
            SetSyncVarDirtyBit(64UL);
            _textFormat = value;
        }
    }


    public TextToyComponent(NetworkObject networkObject) : base(networkObject, new SyncListObject<string>())
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteVector2(_displaySize);
            writer.WriteString(_textFormat);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 32UL) != 0UL)
        {
            writer.WriteVector2(_displaySize);
        }

        if ((SyncVarDirtyBits & 64UL) != 0UL)
        {
            writer.WriteString(_textFormat);
        }
    }

    void AfterSerialize(NetworkWriter writer, bool initial)
    {
        if (initial)
        {
            writer.WriteUInt(0);
        }
    }

}
