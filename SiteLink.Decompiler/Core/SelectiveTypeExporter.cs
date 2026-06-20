using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Metadata;
using SiteLink.Decompiler.Core.Selectors;

namespace SiteLink.Decompiler.Core
{
    public sealed class SelectiveTypeExporter
    {
        private readonly string _assemblyPath;
        private readonly string _outputDirectory;
        private readonly CSharpDecompiler _decompiler;

        public SelectiveTypeExporter(
            string assemblyPath,
            string outputDirectory,
            DecompilerSettings? settings = null)
        {
            _assemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
            _outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));

            string managedDir = Path.GetDirectoryName(_assemblyPath)
                ?? throw new InvalidOperationException("Assembly directory not found.");

            var resolver = new UniversalAssemblyResolver(
                _assemblyPath,
                throwOnError: false,
                targetFramework: null);

            settings ??= new DecompilerSettings(LanguageVersion.CSharp7_3)
            {
                AlwaysUseGlobal = true
            };

            _decompiler = new CSharpDecompiler(_assemblyPath, resolver, settings);
        }

        public void Export(DecompilationPlan plan)
        {
            Directory.CreateDirectory(_outputDirectory);

            foreach (var typeOptions in plan.Types)
            {
                try
                {
                    ExportType(typeOptions);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to export '{typeOptions.ReflectionName}': {ex}");
                }
            }
        }

        private void ExportType(TypeExportOptions options)
        {
            var type = _decompiler.TypeSystem.MainModule.TypeDefinitions
                .FirstOrDefault(t => string.Equals(
                    t.FullTypeName.ReflectionName,
                    options.ReflectionName,
                    StringComparison.Ordinal));

            if (type == null)
            {
                Console.WriteLine($"Type not found: {options.ReflectionName}");
                return;
            }

            var tree = _decompiler.DecompileType(type.FullTypeName);
            var typeDecl = tree.Descendants.OfType<TypeDeclaration>()
                .FirstOrDefault(t => t.Name == type.Name);

            if (typeDecl == null)
            {
                Console.WriteLine($"Could not find syntax node for: {options.ReflectionName}");
                return;
            }

            if (!options.IncludeWholeType)
            {
                FilterMembers(typeDecl, options);
                RewriteSelectedMembers(typeDecl, options);
            }

            string namespacePath = string.IsNullOrWhiteSpace(type.Namespace)
                ? string.Empty
                : type.Namespace.Replace('.', Path.DirectorySeparatorChar);

            string dir = Path.Combine(_outputDirectory, namespacePath);
            Directory.CreateDirectory(dir);

            string filePath = Path.Combine(dir, $"{type.Name}.cs");
            File.WriteAllText(filePath, tree.ToString());

            Console.WriteLine($"Saved: {filePath}");
        }

        private static void FilterMembers(TypeDeclaration typeDecl, TypeExportOptions options)
        {
            foreach (var member in typeDecl.Members.ToList())
            {
                bool keep = member switch
                {
                    ConstructorDeclaration ctor => MatchesConstructor(ctor, options.Constructors),

                    MethodDeclaration method => options.IncludeAllMembers || MatchesMethod(method, options.Methods),

                    PropertyDeclaration prop => options.Properties.Any(p =>
                        string.Equals(p.Name, prop.Name, StringComparison.Ordinal)),

                    IndexerDeclaration => false,

                    CustomEventDeclaration ev => options.Events.Contains(ev.Name),

                    EventDeclaration ev => ev.Variables.Any(v => options.Events.Contains(v.Name)),

                    FieldDeclaration field => field.Variables.Any(v => options.Fields.Contains(v.Name)),

                    TypeDeclaration nested => options.IncludeAllNestedTypes || options.NestedTypes.Contains(nested.Name),

                    EnumMemberDeclaration => true, // keep all enum members if the type is included

                    _ => false
                };

                if (!keep)
                    member.Remove();
            }
        }

        private static bool MatchesConstructor(
            ConstructorDeclaration ctor,
            IReadOnlyCollection<ConstructorSelector> selectors)
        {
            if (selectors.Count == 0)
                return false;

            var parameterTypes = ctor.Parameters
                .Select(GetParameterTypeName)
                .ToArray();

            Console.WriteLine(ctor.GetType().FullName + " " + string.Join(" ", parameterTypes));

            foreach (var selector in selectors)
            {
                if (selector.ParameterTypeNames.Count != parameterTypes.Length)
                    continue;

                bool allMatch = true;
                for (int i = 0; i < parameterTypes.Length; i++)
                {
                    if (!string.Equals(selector.ParameterTypeNames[i], parameterTypes[i], StringComparison.Ordinal))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch)
                    return true;
            }

            return false;
        }

        private static bool MatchesMethod(
            MethodDeclaration method,
            IReadOnlyCollection<MethodSelector> selectors)
        {
            if (selectors.Count == 0)
                return false;

            var parameterTypes = method.Parameters
                .Select(GetParameterTypeName)
                .ToArray();

            return selectors.Any(s => s.Matches(method.Name, parameterTypes));
        }

        private static string GetParameterTypeName(ParameterDeclaration p)
        {
            return p.Type.ToString();
        }

        private static void RewriteSelectedMembers(TypeDeclaration typeDecl, TypeExportOptions options)
        {
            foreach (var member in typeDecl.Members)
            {
                if (member is MethodDeclaration method)
                {
                    var parameterTypes = method.Parameters.Select(GetParameterTypeName).ToArray();

                    var selector = options.Methods.FirstOrDefault(s => s.Matches(method.Name, parameterTypes));
                    if (selector?.ForcedReturnExpression != null)
                    {
                        ForceMethodReturn(method, selector.ForcedReturnExpression);
                    }
                }
                else if (member is PropertyDeclaration prop)
                {
                    var selector = options.Properties.FirstOrDefault(p =>
                        string.Equals(p.Name, prop.Name, StringComparison.Ordinal));

                    if (selector != null)
                    {
                        if (selector.GetterReturnExpression != null)
                            ForcePropertyGetterReturn(prop, selector.GetterReturnExpression);

                        if (selector.SetterBodyStatement != null)
                            ForcePropertySetterBody(prop, selector.SetterBodyStatement);
                    }
                }
            }
        }

        private static void ForceMethodReturn(MethodDeclaration method, string expressionText)
        {
            method.Body = null;

            // void method: ignore or replace with empty body
            if (method.ReturnType.ToString() == "void")
            {
                method.Body = new BlockStatement();
                return;
            }

            Expression expr = ParseExpression(expressionText);
            method.Body = new BlockStatement
            {
                new ReturnStatement(expr)
            };
        }

        private static void ForcePropertyGetterReturn(PropertyDeclaration prop, string expressionText)
        {
            var getter = prop.Getter;
            if (getter == null)
                return;

            getter.Body = null;

            getter.Body = new BlockStatement
            {
                new ReturnStatement(ParseExpression(expressionText))
            };
        }

        private static void ForcePropertySetterBody(PropertyDeclaration prop, string statementText)
        {
            var setter = prop.Setter;
            if (setter == null)
                return;

            setter.Body = null;

            // basic support:
            // "return;"
            // "_field = value;"
            // ""
            if (string.IsNullOrWhiteSpace(statementText))
            {
                setter.Body = new BlockStatement();
                return;
            }

            setter.Body = new BlockStatement
            {
                ParseStatement(statementText)
            };
        }

        private static Expression ParseExpression(string text)
        {
            // simple parser support for common cases
            text = text.Trim();

            return text switch
            {
                "default" => new PrimitiveExpression(null), // fallback-ish; see note below
                "null" => new NullReferenceExpression(),
                "true" => new PrimitiveExpression(true),
                "false" => new PrimitiveExpression(false),
                _ when int.TryParse(text, out int i) => new PrimitiveExpression(i),
                _ when long.TryParse(text, out long l) => new PrimitiveExpression(l),
                _ when double.TryParse(text, out double d) => new PrimitiveExpression(d),
                _ when text.StartsWith("\"") && text.EndsWith("\"") => new PrimitiveExpression(text[1..^1]),
                _ => new IdentifierExpression(text)
            };
        }

        private static Statement ParseStatement(string text)
        {
            text = text.Trim();

            if (string.Equals(text, "return;", StringComparison.Ordinal))
                return new ReturnStatement();

            if (text.EndsWith(";"))
                text = text[..^1];

            int eq = text.IndexOf('=');
            if (eq > 0)
            {
                string left = text[..eq].Trim();
                string right = text[(eq + 1)..].Trim();

                return new ExpressionStatement(
                    new AssignmentExpression(
                        new IdentifierExpression(left),
                        AssignmentOperatorType.Assign,
                        ParseExpression(right)));
            }

            return new EmptyStatement();
        }
    }
}
