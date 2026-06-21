using SiteLink.API.Networking.Connections;

namespace SiteLink.API.Commands.Console
{
    public class EffectCommand
    {
        [ConsoleCommand("effect")]
        public static void OnEffectCommand(string[] args)
        {
            if (args.Length < 1)
            {
                SiteLinkLogger.Info(TranslationManager.Command("effect.usage"), "effect");
                return;
            }

            string userId = args[0];

            if (!RemoteConnection.TryGet(userId, out RemoteConnection client))
            {
                SiteLinkLogger.Info(TranslationManager.Command(
                    "player.not_found",
                    new TranslationContext().With("user_id", userId)), "effect");
                return;
            }

            if (!int.TryParse(args[1], out int effectId))
            {
                return;
            }

            if (!byte.TryParse(args[2], out byte effectValue))
            {
                return;
            }

            client.Session.Player.PlayerEffectsController.SetSyncObjectDirtyBit(1);

            if (client.Session.Player.PlayerEffectsController.SyncObjects[0] is SyncListObject<byte> b)
            {
                b.Set(effectId, effectValue);

                client.Session.Player.SendUpdate(client.Session);
            }

            SiteLinkLogger.Info(TranslationManager.Command("effect.complete"), "effect");
        }
    }
}
