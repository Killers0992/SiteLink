using Mono.CecilX;

namespace SiteLink.Decompiler.Weaver
{
    public static class Resolvers
    {
        public static MethodReference ResolveMethod(TypeReference tr, AssemblyDefinition assembly, Logger Log, string name, ref bool WeavingFailed)
        {
            if (tr == null)
            {
                Log.Error($"Cannot resolve method {name} without a class");
                WeavingFailed = true;
                return null;
            }
            MethodReference method = ResolveMethod(tr, assembly, m => m.Name == name, ref WeavingFailed);
            if (method == null)
            {
                Log.Error($"Method not found with name {name} in type {tr.Name}", tr);
                WeavingFailed = true;
            }
            return method;
        }

        public static MethodReference ResolveMethod(TypeReference t, AssemblyDefinition assembly, System.Func<MethodDefinition, bool> predicate, ref bool WeavingFailed)
        {
            foreach (MethodDefinition methodRef in t.Resolve().Methods)
            {
                if (predicate(methodRef))
                {
                    return assembly.MainModule.ImportReference(methodRef);
                }
            }

            WeavingFailed = true;
            return null;
        }

        public static FieldReference ResolveField(TypeReference tr, AssemblyDefinition assembly, string name, ref bool WeavingFailed)
        {
            if (tr == null)
            {
                WeavingFailed = true;
                return null;
            }
            FieldReference field = ResolveField(tr, assembly, m => m.Name == name, ref WeavingFailed);
            if (field == null)
            {
                WeavingFailed = true;
            }
            return field;
        }

        public static FieldReference ResolveField(TypeReference t, AssemblyDefinition assembly, System.Func<FieldDefinition, bool> predicate, ref bool WeavingFailed)
        {
            foreach (FieldDefinition fieldRef in t.Resolve().Fields)
            {
                if (predicate(fieldRef))
                {
                    return assembly.MainModule.ImportReference(fieldRef);
                }
            }

            WeavingFailed = true;
            return null;
        }

        public static MethodReference TryResolveMethodInParents(TypeReference tr, AssemblyDefinition assembly, string name)
        {
            if (tr == null)
            {
                return null;
            }
            foreach (MethodDefinition methodDef in tr.Resolve().Methods)
            {
                if (methodDef.Name == name)
                {
                    MethodReference methodRef = methodDef;
                    if (tr.IsGenericInstance)
                    {
                        methodRef = methodRef.MakeHostInstanceGeneric(tr.Module, (GenericInstanceType)tr);
                    }
                    return assembly.MainModule.ImportReference(methodRef);
                }
            }

            // Could not find the method in this class,  try the parent
            return TryResolveMethodInParents(tr.Resolve().BaseType.ApplyGenericParameters(tr), assembly, name);
        }

        public static MethodDefinition ResolveDefaultPublicCtor(TypeReference variable)
        {
            foreach (MethodDefinition methodRef in variable.Resolve().Methods)
            {
                if (methodRef.Name == ".ctor" &&
                    methodRef.Resolve().IsPublic &&
                    methodRef.Parameters.Count == 0)
                {
                    return methodRef;
                }
            }
            return null;
        }

        public static MethodReference ResolveProperty(TypeReference tr, AssemblyDefinition assembly, string name)
        {
            foreach (PropertyDefinition pd in tr.Resolve().Properties)
            {
                if (pd.Name == name)
                {
                    return assembly.MainModule.ImportReference(pd.GetMethod);
                }
            }
            return null;
        }
    }
}
