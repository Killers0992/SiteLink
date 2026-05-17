using static LightContainmentZoneDecontamination.DecontaminationController;

namespace SiteLink.API.Networking.Components;

public class DecontaminationControllerComponent : BehaviourComponent
{
    private double _roundStartTime;

    private string _elevatorsLockedText;

    private DecontaminationStatus _decontaminationOverride;

    private float _timeOffset;

    public double RoundStartTime
    {
        get => _roundStartTime;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _roundStartTime = value;
        }
    }

    public string ElevatorsLockedText
    {
        get => _elevatorsLockedText;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _elevatorsLockedText = value;
        }
    }

    public DecontaminationStatus DecontaminationOverride
    {
        get => _decontaminationOverride;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _decontaminationOverride = value;
        }
    }

    public float TimeOffset
    {
        get => _timeOffset;
        set
        {
            SetSyncVarDirtyBit(8UL);
            _timeOffset = value;
        }
    }

    public DecontaminationControllerComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public DecontaminationControllerComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteDouble(_roundStartTime);
            writer.WriteString(_elevatorsLockedText);
            writer.Write(_decontaminationOverride);
            writer.WriteFloat(_timeOffset);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteDouble(_roundStartTime);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteString(_elevatorsLockedText);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.Write(_decontaminationOverride);
        }

        if ((SyncVarDirtyBits & 8UL) != 0UL)
        {
            writer.WriteFloat(_timeOffset);
        }
    }

}
