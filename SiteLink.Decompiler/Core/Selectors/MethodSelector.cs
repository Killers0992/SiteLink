namespace SiteLink.Decompiler.Core.Selectors
{
    public sealed class MethodSelector : IEquatable<MethodSelector>
    {
        public string Name { get; }
        public IReadOnlyList<string>? ParameterTypeNames { get; }

        // Example: "default", "false", "0", "null", "\"hello\""
        public string? ForcedReturnExpression { get; private set; }

        public MethodSelector(string name, IEnumerable<string>? parameterTypeNames = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ParameterTypeNames = parameterTypeNames?.ToArray();
        }

        public MethodSelector ForceReturn(string expression)
        {
            ForcedReturnExpression = expression;
            return this;
        }

        public bool Matches(string methodName, IReadOnlyList<string> parameterTypeNames)
        {
            if (!string.Equals(Name, methodName, StringComparison.Ordinal))
                return false;

            if (ParameterTypeNames == null)
                return true;

            if (ParameterTypeNames.Count != parameterTypeNames.Count)
                return false;

            for (int i = 0; i < ParameterTypeNames.Count; i++)
            {
                if (!string.Equals(ParameterTypeNames[i], parameterTypeNames[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        public bool Equals(MethodSelector? other)
        {
            if (other == null || !string.Equals(Name, other.Name, StringComparison.Ordinal))
                return false;

            if (ParameterTypeNames == null && other.ParameterTypeNames == null)
                return true;

            if (ParameterTypeNames == null || other.ParameterTypeNames == null)
                return false;

            if (ParameterTypeNames.Count != other.ParameterTypeNames.Count)
                return false;

            for (int i = 0; i < ParameterTypeNames.Count; i++)
            {
                if (!string.Equals(ParameterTypeNames[i], other.ParameterTypeNames[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as MethodSelector);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Name, StringComparer.Ordinal);
            if (ParameterTypeNames != null)
            {
                foreach (var p in ParameterTypeNames)
                    hash.Add(p, StringComparer.Ordinal);
            }
            return hash.ToHashCode();
        }
    }
}
