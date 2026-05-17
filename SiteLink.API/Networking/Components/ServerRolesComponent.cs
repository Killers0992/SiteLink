namespace SiteLink.API.Networking.Components;

public class ServerRolesComponent : BehaviourComponent
{
    private string _myText;

    private string _myColor;

    private string _globalBadge;

    private string _globalBadgeSignature;

    private bool _hideFromPlayerList;

    public string MyText
    {
        get => _myText;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _myText = value;
        }
    }

    public string MyColor
    {
        get => _myColor;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _myColor = value;
        }
    }

    public string GlobalBadge
    {
        get => _globalBadge;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _globalBadge = value;
        }
    }

    public string GlobalBadgeSignature
    {
        get => _globalBadgeSignature;
        set
        {
            SetSyncVarDirtyBit(8UL);
            _globalBadgeSignature = value;
        }
    }

    public bool HideFromPlayerList
    {
        get => _hideFromPlayerList;
        set
        {
            SetSyncVarDirtyBit(16UL);
            _hideFromPlayerList = value;
        }
    }

    public ServerRolesComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public ServerRolesComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteString(_myText);
            writer.WriteString(_myColor);
            writer.WriteString(_globalBadge);
            writer.WriteString(_globalBadgeSignature);
            writer.WriteBool(_hideFromPlayerList);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteString(_myText);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteString(_myColor);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteString(_globalBadge);
        }

        if ((SyncVarDirtyBits & 8UL) != 0UL)
        {
            writer.WriteString(_globalBadgeSignature);
        }

        if ((SyncVarDirtyBits & 16UL) != 0UL)
        {
            writer.WriteBool(_hideFromPlayerList);
        }
    }

}
