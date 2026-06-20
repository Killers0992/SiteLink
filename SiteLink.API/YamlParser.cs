using SiteLink.API.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.ObjectGraphVisitors;

namespace SiteLink.API
{
    /// <summary>
    /// Handles everything regarding parsing .yml files, both serializing and deserializing them.
    /// </summary>
    public static class YamlParser
    {
        /// <summary>
        /// The serializer instance used for serializing plugin and server configs.
        /// </summary>
        public static ISerializer Serializer { get; } = new SerializerBuilder()
            .WithTypeInspector(typeInspector => new CommentGatheringTypeInspector(typeInspector))
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .DisableAliases()
            .IgnoreFields()
            .Build();

        /// <summary>
        /// The serializer instance used for deserializing plugin and server configs.
        /// </summary>
        public static IDeserializer Deserializer { get; } = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .IgnoreFields()
            .Build();

    }
}
