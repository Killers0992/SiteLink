using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class NicknameSyncComponent : BehaviourComponent
{
    private float _viewRange;

    private string _customPlayerInfoString;

    private PlayerInfoArea _playerInfoToShow;

    private string _myNickSync;

    private string _displayName;

    public float ViewRange
    {
        get => _viewRange;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _viewRange = value;
        }
    }

    public string CustomPlayerInfoString
    {
        get => _customPlayerInfoString;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _customPlayerInfoString = value;
        }
    }

    public PlayerInfoArea PlayerInfoToShow
    {
        get => _playerInfoToShow;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _playerInfoToShow = value;
        }
    }

    public string MyNickSync
    {
        get => _myNickSync;
        set
        {
            SetSyncVarDirtyBit(8UL);
            _myNickSync = value;
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            SetSyncVarDirtyBit(16UL);
            _displayName = value;
        }
    }

    public NicknameSyncComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public NicknameSyncComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    public override bool OnReceiveCommand(ushort functionHash, NetworkReader reader)
    {
        switch (functionHash)
        {
            case NetworkMessages.NicknameSync.Commands.SetNick:

                MyNickSync = reader.ReadString();
                Object.SendUpdate();
                return false;
        }

        return base.OnReceiveCommand(functionHash, reader);
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteFloat(_viewRange);
            writer.WriteString(_customPlayerInfoString);
            writer.Write(_playerInfoToShow);
            writer.WriteString(_myNickSync);
            writer.WriteString(_displayName);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteFloat(_viewRange);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteString(_customPlayerInfoString);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.Write(_playerInfoToShow);
        }

        if ((SyncVarDirtyBits & 8UL) != 0UL)
        {
            writer.WriteString(_myNickSync);
        }

        if ((SyncVarDirtyBits & 16UL) != 0UL)
        {
            writer.WriteString(_displayName);
        }
    }

}
