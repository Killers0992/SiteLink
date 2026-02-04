namespace SiteLink.Core;

public class ChallengeHandler
{
    public int ClientChallengeId;
    public ushort ClientChallengeSecretLen;
    public byte[] ClientChallenge, ClientChallengeBase, ClientChallengeResponse;

    public Session Session;

    public ChallengeHandler(Session session)
    {
        Session = session;
    }

    public void ProcessChallenge(NetPacketReader reader)
    {
        if (!reader.TryGetByte(out byte mode) || !reader.TryGetInt(out ClientChallengeId))
            return;

        ChallengeType challengeType = (ChallengeType)mode;

        switch (challengeType)
        {
            case ChallengeType.Reply:
                if (reader.TryGetBytesWithLength(out ClientChallengeResponse))
                {
                    SiteLinkLogger.Info("Reconnect back");
                    Session.Connect(ClientChallengeId, ClientChallengeResponse);
                }
                break;

            case ChallengeType.MD5:
            case ChallengeType.SHA1:
                if (reader.TryGetBytesWithLength(out ClientChallengeBase) &&
                    reader.TryGetUShort(out ClientChallengeSecretLen) &&
                    reader.TryGetBytesWithLength(out ClientChallenge))
                {
                    SiteLinkLogger.Error($"Received challenge {challengeType} which is not supported");
                }
                break;
        }
    }
}
