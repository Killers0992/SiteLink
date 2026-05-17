namespace SiteLink.API.Networking.Components;

public class LightSourceToyComponent : AdminToyBaseComponent
{
    private float _lightIntensity;

    private float _lightRange;

    private Color _lightColor;

    private LightShadows _shadowType;

    private float _shadowStrength;

    private LightType _lightType;

    private LightShape _lightShape;

    private float _spotAngle;

    private float _innerSpotAngle;

    public float LightIntensity
    {
        get => _lightIntensity;
        set
        {
            SetSyncVarDirtyBit(32UL);
            _lightIntensity = value;
        }
    }

    public float LightRange
    {
        get => _lightRange;
        set
        {
            SetSyncVarDirtyBit(64UL);
            _lightRange = value;
        }
    }

    public Color LightColor
    {
        get => _lightColor;
        set
        {
            SetSyncVarDirtyBit(128UL);
            _lightColor = value;
        }
    }

    public LightShadows ShadowType
    {
        get => _shadowType;
        set
        {
            SetSyncVarDirtyBit(256UL);
            _shadowType = value;
        }
    }

    public float ShadowStrength
    {
        get => _shadowStrength;
        set
        {
            SetSyncVarDirtyBit(512UL);
            _shadowStrength = value;
        }
    }

    public LightType LightType
    {
        get => _lightType;
        set
        {
            SetSyncVarDirtyBit(1024UL);
            _lightType = value;
        }
    }

    public LightShape LightShape
    {
        get => _lightShape;
        set
        {
            SetSyncVarDirtyBit(2048UL);
            _lightShape = value;
        }
    }

    public float SpotAngle
    {
        get => _spotAngle;
        set
        {
            SetSyncVarDirtyBit(4096UL);
            _spotAngle = value;
        }
    }

    public float InnerSpotAngle
    {
        get => _innerSpotAngle;
        set
        {
            SetSyncVarDirtyBit(8192UL);
            _innerSpotAngle = value;
        }
    }

    public LightSourceToyComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteFloat(_lightIntensity);
            writer.WriteFloat(_lightRange);
            writer.WriteColor(_lightColor);
            writer.Write(_shadowType);
            writer.WriteFloat(_shadowStrength);
            writer.Write(_lightType);
            writer.Write(_lightShape);
            writer.WriteFloat(_spotAngle);
            writer.WriteFloat(_innerSpotAngle);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 32UL) != 0UL)
        {
            writer.WriteFloat(_lightIntensity);
        }

        if ((SyncVarDirtyBits & 64UL) != 0UL)
        {
            writer.WriteFloat(_lightRange);
        }

        if ((SyncVarDirtyBits & 128UL) != 0UL)
        {
            writer.WriteColor(_lightColor);
        }

        if ((SyncVarDirtyBits & 256UL) != 0UL)
        {
            writer.Write(_shadowType);
        }

        if ((SyncVarDirtyBits & 512UL) != 0UL)
        {
            writer.WriteFloat(_shadowStrength);
        }

        if ((SyncVarDirtyBits & 1024UL) != 0UL)
        {
            writer.Write(_lightType);
        }

        if ((SyncVarDirtyBits & 2048UL) != 0UL)
        {
            writer.Write(_lightShape);
        }

        if ((SyncVarDirtyBits & 4096UL) != 0UL)
        {
            writer.WriteFloat(_spotAngle);
        }

        if ((SyncVarDirtyBits & 8192UL) != 0UL)
        {
            writer.WriteFloat(_innerSpotAngle);
        }
    }

}
