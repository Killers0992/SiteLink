
namespace SiteLink.API.Networking.Objects;

//
// Name: TextToy
// NetworkID: 0
// AssetID: 162530276
// SceneID: 0
// Path: TextToy
//
public class TextToyObject : NetworkObject
{
    public const uint ObjectAssetId = 162530276;

    public override uint AssetId { get; } = ObjectAssetId;
    public TextToyComponent TextToy { get; }

    public TextToyObject(World world, Session owner = null, uint networkId = 0) : base(world, owner, networkId)
    {
        //
        Behaviours = new BehaviourComponent[1];

        TextToy = new TextToyComponent(this);
        Behaviours[0] = TextToy;
    }

    /// <summary>
    /// Sends a text value rendered specifically for one observer.
    /// The shared text value is restored immediately afterwards.
    /// </summary>
    public void SendText(
        Session observer,
        string template,
        TranslationContext context = null)
    {
        if (observer?.Connection == null)
            return;

        context ??= TranslationContext.For(observer);
        string previous = TextToy.TextFormat;
        TextToy.TextFormat = TranslationManager.Format(template, context).Format();
        SendUpdate(observer);
        TextToy.TextFormat = previous;
        TextToy.ClearAllDirtyBits();
    }

    public void SendLocalizedText(
        Session observer,
        Func<LanguageTranslations, string> selector,
        TranslationContext context = null) =>
        SendText(observer, selector(TranslationManager.For(observer)), context);
}
