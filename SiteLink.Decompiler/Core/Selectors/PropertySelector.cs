namespace SiteLink.Decompiler.Core.Selectors
{
    public sealed class PropertySelector : IEquatable<PropertySelector>
    {
        public string Name { get; }

        // null = keep original body
        public string? GetterReturnExpression { get; private set; }
        public string? SetterBodyStatement { get; private set; }

        public PropertySelector(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public PropertySelector ForceGetterReturn(string expression)
        {
            GetterReturnExpression = expression;
            return this;
        }

        public PropertySelector ForceSetterBody(string statement)
        {
            SetterBodyStatement = statement;
            return this;
        }

        public bool Equals(PropertySelector? other)
            => other != null && string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as PropertySelector);

        public override int GetHashCode()
            => StringComparer.Ordinal.GetHashCode(Name);
    }
}
