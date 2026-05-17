namespace SiteLink.Decompiler.Core.Selectors
{
    public sealed class ConstructorSelector : IEquatable<ConstructorSelector>
    {
        public IReadOnlyList<string> ParameterTypeNames { get; }

        public ConstructorSelector(IEnumerable<string>? parameterTypeNames = null)
        {
            ParameterTypeNames = (parameterTypeNames ?? Array.Empty<string>()).ToArray();
        }

        public bool Equals(ConstructorSelector? other)
        {
            if (other == null || ParameterTypeNames.Count != other.ParameterTypeNames.Count)
                return false;

            for (int i = 0; i < ParameterTypeNames.Count; i++)
            {
                if (!string.Equals(ParameterTypeNames[i], other.ParameterTypeNames[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as ConstructorSelector);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var p in ParameterTypeNames)
                hash.Add(p, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }
}
