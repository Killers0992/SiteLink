using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class CapybaraToyComponent : AdminToyBaseComponent
{
    private bool _collisionsEnabled;

    public bool CollisionsEnabled
    {
        get => _collisionsEnabled;
        set
        {
            SetSyncVarDirtyBit(32UL);
            _collisionsEnabled = value;
        }
    }

    public CapybaraToyComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteBool(_collisionsEnabled);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 32UL) != 0UL)
        {
            writer.WriteBool(_collisionsEnabled);
        }
    }

}
