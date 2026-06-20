using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class ServerConfigSynchronizerComponent : BehaviourComponent
{
    private byte _mainBoolsSync;

    private string _serverName;

    private bool _enableRemoteAdminPredefinedBanTemplates;

    private string _remoteAdminExternalPlayerLookupMode;

    private string _remoteAdminExternalPlayerLookupURL;

    public byte MainBoolsSync
    {
        get => _mainBoolsSync;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _mainBoolsSync = value;
        }
    }

    public string ServerName
    {
        get => _serverName;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _serverName = value;
        }
    }

    public bool EnableRemoteAdminPredefinedBanTemplates
    {
        get => _enableRemoteAdminPredefinedBanTemplates;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _enableRemoteAdminPredefinedBanTemplates = value;
        }
    }

    public string RemoteAdminExternalPlayerLookupMode
    {
        get => _remoteAdminExternalPlayerLookupMode;
        set
        {
            SetSyncVarDirtyBit(8UL);
            _remoteAdminExternalPlayerLookupMode = value;
        }
    }

    public string RemoteAdminExternalPlayerLookupURL
    {
        get => _remoteAdminExternalPlayerLookupURL;
        set
        {
            SetSyncVarDirtyBit(16UL);
            _remoteAdminExternalPlayerLookupURL = value;
        }
    }

    public ServerConfigSynchronizerComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public ServerConfigSynchronizerComponent(NetworkObject networkObject) : this(networkObject, new SyncListObject<sbyte>(), new SyncListObject<AmmoLimit>(), new SyncListObject<PredefinedBanTemplate>())
    {
        //
    }

    public struct PredefinedBanTemplate
    {
        // Token: 0x040002E7 RID: 743
        public int Duration;

        // Token: 0x040002E8 RID: 744
        public string FormattedDuration;

        // Token: 0x040002E9 RID: 745
        public string Reason;
    }

    public struct AmmoLimit
    {
        // Token: 0x040002E5 RID: 741
        public ItemType AmmoType;

        // Token: 0x040002E6 RID: 742
        public ushort Limit;
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteByte(_mainBoolsSync);
            writer.WriteString(_serverName);
            writer.WriteBool(_enableRemoteAdminPredefinedBanTemplates);
            writer.WriteString(_remoteAdminExternalPlayerLookupMode);
            writer.WriteString(_remoteAdminExternalPlayerLookupURL);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteByte(_mainBoolsSync);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteString(_serverName);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteBool(_enableRemoteAdminPredefinedBanTemplates);
        }

        if ((SyncVarDirtyBits & 8UL) != 0UL)
        {
            writer.WriteString(_remoteAdminExternalPlayerLookupMode);
        }

        if ((SyncVarDirtyBits & 16UL) != 0UL)
        {
            writer.WriteString(_remoteAdminExternalPlayerLookupURL);
        }
    }

}
