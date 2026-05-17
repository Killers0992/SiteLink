using SiteLink.Decompiler.Core.Selectors;

namespace SiteLink.Decompiler.Core
{
    public sealed class TypeExportOptions
    {
        public string ReflectionName { get; }

        public HashSet<string> Fields { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Events { get; } = new(StringComparer.Ordinal);
        public HashSet<string> NestedTypes { get; } = new(StringComparer.Ordinal);

        public List<ConstructorSelector> Constructors { get; } = new();
        public List<MethodSelector> Methods { get; } = new();
        public List<PropertySelector> Properties { get; } = new();

        public bool IncludeWholeType { get; private set; }
        public bool IncludeAllNestedTypes { get; private set; }

        public bool IncludeAllMembers { get; private set; }

        public TypeExportOptions(string reflectionName)
        {
            ReflectionName = reflectionName ?? throw new ArgumentNullException(nameof(reflectionName));
        }

        public TypeExportOptions AddField(string name)
        {
            Fields.Add(name);
            return this;
        }

        public TypeExportOptions AddEvent(string name)
        {
            Events.Add(name);
            return this;
        }

        public TypeExportOptions AddNestedType(string name)
        {
            NestedTypes.Add(name);
            return this;
        }

        public TypeExportOptions AddConstructor(params string[] parameterTypeNames)
        {
            Constructors.Add(new ConstructorSelector(parameterTypeNames));
            return this;
        }

        public TypeExportOptions AddMethod(string name, string? forcedReturnExpression = null, params string[] parameterTypeNames)
        {
            var selector = new MethodSelector(name, parameterTypeNames);
            if (!string.IsNullOrWhiteSpace(forcedReturnExpression))
                selector.ForceReturn(forcedReturnExpression);

            Methods.Add(selector);
            return this;
        }

        public TypeExportOptions AddAllMethods()
        {
            IncludeAllMembers = true;
            return this;
        }

        public TypeExportOptions AddProperty(string name, string? getterReturnExpression = null, string? setterBodyStatement = null)
        {
            var selector = new PropertySelector(name);

            if (!string.IsNullOrWhiteSpace(getterReturnExpression))
                selector.ForceGetterReturn(getterReturnExpression);

            if (!string.IsNullOrWhiteSpace(setterBodyStatement))
                selector.ForceSetterBody(setterBodyStatement);

            Properties.Add(selector);
            return this;
        }

        public TypeExportOptions KeepWholeType(bool value = true)
        {
            IncludeWholeType = value;
            return this;
        }

        public TypeExportOptions KeepAllNestedTypes(bool value = true)
        {
            IncludeAllNestedTypes = value;
            return this;
        }
    }
}
