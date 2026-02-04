using Hints;
using Mirror;
using PlayerRoles;
using RelativePositioning;
using SiteLink.API.Networking;
using UnityEngine.SceneManagement;
using UserSettings.ServerSpecific;
using YamlDotNet.Core.Tokens;
using static PlayerStatsSystem.SyncedStatMessages;

namespace SiteLink.API.Misc
{
    public static class MirrorMessagesEx
    {
        static int _entriesVersion;

        public static void Spawn(this MirrorSender sender, uint networkId, bool isLocalPlayer, bool isOwner, ulong sceneId, uint assetId, Vector3 position, Quaternion rotation, Vector3 scale, ArraySegment<byte> payload)
        {
            sender.Send(w =>
            {
                w.WriteUShort(NetworkMessageId<SpawnMessage>.Id);

                w.WriteUInt(networkId);

                // IsLocalPlayer
                w.WriteBool(isLocalPlayer);
                // IsOwner
                w.WriteBool(isOwner);

                w.WriteULong(sceneId);
                w.WriteUInt(assetId);

                w.WriteVector3(position);
                w.WriteQuaternion(rotation);
                w.WriteVector3(scale);

                w.WriteArraySegmentAndSize(payload);
            });
        }

        public static void Seed(this MirrorSender sender, int seed)
        {
            sender.Send(w =>
            {
                w.WriteUShort(NetworkMessages.SeedMessage);
                w.WriteInt(seed);
            });
        }

        public static void Scene(this MirrorSender sender, string sceneName, byte op = 0, bool customHandling = false)
        {
            sender.Send(w =>
            {
                w.WriteUShort(NetworkMessages.SceneMessage);

                w.WriteString(sceneName);
                w.WriteByte(op);
                w.WriteBool(customHandling);
            });
        }

        public static void Reconnect(this MirrorSender sender)
        {
            sender.Send(w =>
            {
                w.WriteUShort(NetworkMessages.RoundRestartMessage);

                //Restart Type ( Full Restart )
                w.WriteByte(0);
                w.WriteBool(true);
                w.WriteBool(false);
                w.WriteFloat(1f);
            });
        }

        public static void Hint(this MirrorSender sender, string message, float duration = 3)
        {
            sender.Send(w =>
            {
                w.WriteUShort(NetworkMessageId<HintMessage>.Id);

                // TextHint
                w.WriteByte(1);

                // Duration
                w.WriteFloat(duration);

                // 0 - Effects
                w.WriteInt(0);

                // 1 - Hint Parameters
                w.WriteInt(1);

                // String Parameter
                w.WriteByte(0);
                w.WriteString(string.Empty);

                // Message
                w.WriteString(message);
            });
        }

        public static void Destroy(this MirrorSender sender, uint networkIdentityId)
        {
            sender.Send(w =>
            {
                w.WriteUShort(NetworkMessages.ObjectDestroyMessage);

                w.WriteUInt(networkIdentityId);
            });
        }

        public static void ServerSpecificEntries(this MirrorSender sender, ServerSpecificSettingBase[] entires)
        {
            sender.Send(w =>
            {
                SSSEntriesPack packed = new SSSEntriesPack(entires, _entriesVersion++);

                w.WriteUShort(NetworkMessages.SSSEntriesPack);
                packed.Serialize(w);
            });
        }

        public static void Health(this MirrorSender sender, uint networkId, float value)
        {
            sender.Send(w =>
            {
                w.WriteUShort(NetworkMessages.StatMessage);

                w.WriteUInt(networkId);

                // 0 HealthStat
                // 1 AhpStat
                // 2 StaminaStat
                // 3 AdminFlagsStat
                // 4 HumeShieldStat
                // 5 Vigor Stat
                w.WriteByte(0);

                w.WriteByte((byte)StatMessageType.CurrentValue);

                int clampedValue = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.CeilToInt(value), 0, 65535);
                w.WriteUShort((ushort)clampedValue);
            });
        }

        public static void Role(this MirrorSender sender, uint networkId, RoleTypeId role)
        {
            sender.Send(w =>
            {
                w.WriteUShort(NetworkMessages.RoleSyncInfo);

                w.WriteUInt(networkId);

                w.WriteSByte((sbyte)role);

                w.WriteRelativePosition(new RelativePosition(new UnityEngine.Vector3(0f, 0f, 0f)));
                w.WriteUShort(0);
            });
        }
    }
}
