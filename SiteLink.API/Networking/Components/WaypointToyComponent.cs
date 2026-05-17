namespace SiteLink.API.Networking.Components;

public class WaypointToyComponent : AdminToyBaseComponent
{
    private bool _visualizeBounds;

    private float _priority;

    private Vector3 _boundsSize;

    public bool VisualizeBounds
    {
        get => _visualizeBounds;
        set
        {
            SetSyncVarDirtyBit(32UL);
            _visualizeBounds = value;
        }
    }

    public float Priority
    {
        get => _priority;
        set
        {
            SetSyncVarDirtyBit(64UL);
            _priority = value;
        }
    }

    public Vector3 BoundsSize
    {
        get => _boundsSize;
        set
        {
            SetSyncVarDirtyBit(128UL);
            _boundsSize = value;
        }
    }

    public byte WaypointId { get; set; }


    public WaypointToyComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    public override void OnSerialize(NetworkWriter writer, bool initialState)
    {
        base.OnSerialize(writer, initialState);

        if (initialState)
        {
            writer.WriteUInt(0);
            writer.WriteByte(WaypointId);
        }
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteBool(_visualizeBounds);
            writer.WriteFloat(_priority);
            writer.WriteVector3(_boundsSize);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 32UL) != 0UL)
        {
            writer.WriteBool(_visualizeBounds);
        }

        if ((SyncVarDirtyBits & 64UL) != 0UL)
        {
            writer.WriteFloat(_priority);
        }

        if ((SyncVarDirtyBits & 128UL) != 0UL)
        {
            writer.WriteVector3(_boundsSize);
        }
    }
}
