namespace SiteLink.API.Commands;

public class SpawnTextCommand
{
    [ConsoleCommand("spawntext")]
    public static void OnSpawnTextCommand( string[] args)
    {
        if (args.Length < 2)
        {
            SiteLinkLogger.Info("Syntax: spawntext <userid> <message>", "spawntext");
            return;
        }

        string userId = args[0];

        if (!Client.TryGet(userId, out Client client))
        {
            SiteLinkLogger.Info($"Not found player with userid (f=green){userId}(f=white)", "spawntext");
            return;
        }

        string message = string.Join(" ", args.Skip(1));

        TextToyObject text = new TextToyObject(client.World)
        {
            Position = client.Position,
        };

        text.TextToy.TextFormat = message;
        text.TextToy.DisplaySize = new Vector2(150f, 25f);

        text.SpawnWithPayload(client);

        SiteLinkLogger.Info($"Spawned text for (f=green){userId}(f=white) with message (f=green){message}(f=white)", "spawntext");
    }
}
