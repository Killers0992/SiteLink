namespace SiteLink.Decompiler.Core
{
    public sealed class DecompilationPlan
    {
        private readonly List<TypeExportOptions> _types = new();

        public IReadOnlyList<TypeExportOptions> Types => _types;

        public DecompilationPlan AddType(TypeExportOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _types.Add(options);
            return this;
        }
    }
}
