using SiteLink.API.Handlers;

namespace SiteLink.API.Structs;

public struct PreAuth
{
    public Version ClientVersion;
    public bool BackwardCompatibility;
    public byte BackwardRevision;

    public ClientType ClientType;

    public string UserId;

    public long Expiration;

    public CentralAuthPreauthFlags CentralFlags;

    public string Country;

    public byte[] Signature;

    public string IpAddress;

    public string SecretKey;
    public Server TargetServer;

    public PreAuth(string secretKey, Server targetServer)
    {
        ClientType = ClientType.Bridge;

        SecretKey = secretKey;

        TargetServer = targetServer;
    }

    public PreAuth(Version clientVersion, ClientType clientType, bool backwardCompatibility, byte backwardRevision, string userId, long expiration, CentralAuthPreauthFlags centralFlags, string country, byte[] signature, string ipAddress)
    {
        ClientVersion = clientVersion;

        ClientType = clientType;

        BackwardCompatibility = backwardCompatibility;
        BackwardRevision = backwardRevision;

        UserId = userId;

        Expiration = expiration;

        CentralFlags = centralFlags;

        Country = country;

        Signature = signature;

        IpAddress = ipAddress;
    }

    public NetDataWriter Create(bool includeIp, int challengeId = 0, byte[] challengeResponse = null)
    {
        NetDataWriter writer = new NetDataWriter();

        writer.Put((byte)ClientType.GameClient);
        writer.Put((byte)ClientVersion.Major);
        writer.Put((byte)ClientVersion.Minor);
        writer.Put((byte)ClientVersion.Build);
        writer.Put(BackwardCompatibility);

        if (BackwardCompatibility)
            writer.Put(BackwardRevision);

        writer.Put(challengeId);

        if (challengeId != 0)
            writer.PutBytesWithLength(challengeResponse);

        writer.Put(UserId);
        writer.Put(Expiration);
        writer.Put((byte)CentralFlags);
        writer.Put(Country);
        writer.PutBytesWithLength(Signature);

        if (includeIp)
        {
            writer.Put(IpAddress);
        }

        return writer;
    }

    public static bool TryRead(Networking.Listener listener, string connectionIp, NetDataReader reader, ref DisconnectType response, ref bool rejectForce, ref PreAuth preAuth)
    {
        if (!reader.TryGetByte(out byte rawClientType))
        {
            rejectForce = true;
            response = DisconnectType.InvalidClientType;
            return false;
        }

        if (!Enum.IsDefined(typeof(ClientType), rawClientType))
        {
            rejectForce = true;
            response = DisconnectType.ClientTypeOutOfRange;
            return false;
        }

        ClientType clientType = (ClientType)rawClientType;

        switch (clientType)
        {
            case ClientType.VerificationService:
                rejectForce = true;
                response = DisconnectType.ForbiddenClientType;
                return false;

            case ClientType.Bridge:
                if (!reader.TryGetString(out string secretKey))
                {
                    rejectForce = true;
                    response = DisconnectType.ForbiddenClientType;
                    return false;
                }

                Server targetServer = Server.RegisteredServers.Values
                    .FirstOrDefault(x =>
                        x.Settings.Bridge.Enabled &&
                        x.Settings.Bridge.SecretKey == secretKey);

                if (targetServer == null)
                {
                    rejectForce = true;
                    response = DisconnectType.ForbiddenClientType;
                    return false;
                }

                preAuth = new PreAuth(secretKey, targetServer);
                response = DisconnectType.Valid;
                return true;
        }

        if (!reader.TryGetByte(out byte major))
        {
            rejectForce = true;
            response = DisconnectType.InvalidMajorVersion;
            return false;
        }

        if (!reader.TryGetByte(out byte minor))
        {
            rejectForce = true;
            response = DisconnectType.InvalidMinorVersion;
            return false;
        }

        if (!reader.TryGetByte(out byte revision))
        {
            rejectForce = true;
            response = DisconnectType.InvalidRevisionVersion;
            return false;
        }


        if (!reader.TryGetBool(out bool backwardCompatibility))
        {
            rejectForce = true;
            response = DisconnectType.InvalidBackwardCompatibility;
            return false;
        }

        byte backwardRevision = 0;

        if (backwardCompatibility)
        {
            if (!reader.TryGetByte(out backwardRevision))
            {
                rejectForce = true;
                response = DisconnectType.InvalidBackwardRevision;
                return false;
            }
        }

        Version clientVersion = new Version(major, minor, revision);

        if (!listener.GameVersion.ValidateGameVersion(clientVersion, backwardCompatibility, backwardRevision))
        {
            response = DisconnectType.VersionNotCompatible;
            return false;
        }

        if (!reader.TryGetInt(out int challengeId))
        {
            rejectForce = true;
            response = DisconnectType.InvalidChallengeId;
            return false;
        }

        // If challengeId is not 0 then client sent challenge response.
        if (challengeId != 0)
        {
            if (!reader.TryGetBytesWithLength(out byte[] challengeResponse))
            {
                rejectForce = true;
                response = DisconnectType.InvalidChallengeResponse;
                return false;
            }
        }

        if (!reader.TryGetString(out string userId))
        {
            rejectForce = true;
            response = DisconnectType.InvalidUserId;
            return false;
        }

        if (string.IsNullOrEmpty(userId))
        {
            rejectForce = true;
            response = DisconnectType.UserIdIsEmpty;
            return false;
        }

        if (!reader.TryGetLong(out long expiration))
        {
            rejectForce = true;
            response = DisconnectType.InvalidExpiration;
            return false;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiration)
        {
            rejectForce = true;
            response = DisconnectType.PreAuthExpired;
            return false;
        }

        if (!reader.TryGetByte(out byte rawCentralFlags))
        {
            rejectForce = true;
            response = DisconnectType.InvalidCentralFlags;
            return false;
        }

        CentralAuthPreauthFlags centralFlags = (CentralAuthPreauthFlags)rawCentralFlags;

        if (!reader.TryGetString(out string region))
        {
            rejectForce = true;
            response = DisconnectType.InvalidRegion;
            return false;
        }

        if (!reader.TryGetBytesWithLength(out byte[] signature))
        {
            rejectForce = true;
            response = DisconnectType.InvalidSignature;
            return false;
        }

        if (!ECDSA.VerifyBytes($"{userId};{rawCentralFlags};{region};{expiration}", signature, ScpServerListHandler.PublicKey))
        {
            rejectForce = true;
            response = DisconnectType.BadSignature;
            return false;
        }

        preAuth = new PreAuth(clientVersion, clientType, backwardCompatibility, backwardRevision, userId, expiration, centralFlags, region, signature, connectionIp);
        response = DisconnectType.Valid;
        return true;
    }
}
