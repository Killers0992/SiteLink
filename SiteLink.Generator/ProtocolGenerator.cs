using Hazards;
using MapGeneration;
using Mirror;
using PlayerRoles.PlayableScps.HumanTracker;
using SiteLink.Generator.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SiteLink.Generator;

public class ProtocolGenerator
{
    static int id = 0;

    static List<uint> WhitelistedObjects = new List<uint>()
    {
        3938583646,
        162530276,
        1321952889,
        3816198336,
        3956448839,
        180257209
    };

    static Dictionary<string, ComponentGenerationInfo> componentMap = new()
    {
        ["AdminToys.WaypointToy"] = new ComponentGenerationInfo
        {
            ExtraProperties = new()
            {
                new CustomPropertyInfo
                {
                    TypeName = "byte",
                    PropertyName = "WaypointId",
                    IsSyncVar = false
                }
            },

            SerializationHooks = new()
            {
                new CustomSerializationInfo
                {
                    MethodName = "AfterSerialize",
                    RunOnInitialOnly = true,
                    WriteCalls = new() { "writer.WriteUInt(0);", "writer.WriteByte(WaypointId);" }
                }
            }
        },
        ["AdminToys.TextToy"] = new ComponentGenerationInfo
        {
            SerializationHooks = new()
            {
                new CustomSerializationInfo
                {
                    MethodName = "AfterSerialize",
                    RunOnInitialOnly = true,
                    WriteCalls = new() { "writer.WriteUInt(0);" }
                }
            }
        }
    };

    public static void Generate(string folder = "D:\\VS Projects\\SiteLink\\SiteLink.API\\Networking")
    {
        string componentsPath = Path.Combine(folder, "Components");
        string objectsPath = Path.Combine(folder, "Objects");

        RegenerateFolder(componentsPath);
        RegenerateFolder(objectsPath);

        List<NetworkIdentity> networkIdentities = ScanForNetworkIdentities();

        Dictionary<uint, string> _assetDb = new Dictionary<uint, string>();

        foreach (NetworkIdentity identity in networkIdentities)
        {
            if (!WhitelistedObjects.Contains(identity.assetId))
                continue;

            string className = identity.gameObject.name.CapitalizeFirst();

            className = className.ReplaceWhitespace(string.Empty);
            className = Regex.Replace(className, "[^a-zA-Z0-9]", "");
            className += "Object";

            GenerateObjectClass(folder, className, identity, out uint assetId);

            if (!_assetDb.ContainsKey(assetId))
                _assetDb.Add(assetId, className);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("namespace SiteLink.API.Networking;");
        sb.AppendLine();
        sb.AppendLine("public static class AssetsDatabase");
        sb.AppendLine("{");
        sb.AppendLine("    public static Dictionary<uint, Func<(World, Session, uint), NetworkObject>> Objects = new Dictionary<uint, Func<(World, Session, uint), NetworkObject>>()");
        sb.AppendLine("    {");

        foreach (var db in _assetDb)
        {
            sb.AppendLine("        { " + db.Key + ", (p) => new " + db.Value + "(p.Item1, p.Item2, p.Item3) },");
        }

        sb.AppendLine("    };");
        sb.AppendLine("}");

        File.WriteAllText(Path.Combine(folder, "AssetsDatabase.cs"), sb.ToString());
    }

    private static void GenerateObjectClass(string path, string className, NetworkIdentity identity, out uint assetId)
    {
        string objectsPath = Path.Combine(path, "Objects");
        string targetPath = Path.Combine(objectsPath, className + ".cs");

        RoomIdentifier room = identity.GetComponentInParent<RoomIdentifier>();
        if (room != null)
        {
            ServerConsole.AddLog($"{room.name} {identity.assetId}");
        }

        if (File.Exists(targetPath))
        {
            id += 1;
            targetPath = Path.Combine(objectsPath, className + $"_{id}.cs");
        }

        StringBuilder sb = new StringBuilder();

        NetworkBehaviour[] behaviours = identity.GetComponentsInChildren<NetworkBehaviour>(includeInactive: true);

        List<BehaviourInfo> behaviourInfos = new List<BehaviourInfo>();

        for (int x = 0; x < behaviours.Length; x++)
        {
            NetworkBehaviour behaviour = behaviours[x];
            Type leafType = behaviour.GetType();

            List<Type> blacklist = new List<Type>()
            {
                typeof(AspectRatioSync),
                typeof(LastHumanTracker),
                typeof(PrismaticCloud),
                typeof(TantrumEnvironmentalHazard)
            };

            if (blacklist.Contains(leafType))
                continue;

            var chain = GetBehaviourChain(leafType).ToList();
            if (chain.Count == 0)
                continue;

            int offset = 0;
            string prevComponent = null;

            List<SyncListInfo> aggregatedSyncLists = new();

            for (int layerIndex = 0; layerIndex < chain.Count; layerIndex++)
            {
                Type layerType = chain[layerIndex];

                var layer = new BehaviourLayerInfo
                {
                    BehaviourType = layerType,
                    ComponentClassName = layerType.Name.ReplaceWhitespace(string.Empty) + "Component",
                    BaseComponentClassName = prevComponent,
                    SyncVarBitOffset = offset,
                };

                var declaredSyncVars = GetDeclaredSyncVars(layerType);
                for (int i = 0; i < declaredSyncVars.Length; i++)
                {
                    FieldInfo field = declaredSyncVars[i];
                    ulong bit = 1UL << (layer.SyncVarBitOffset + i);

                    layer.DeclaredSyncVars.Add(new SyncVarInfo()
                    {
                        Name = field.Name,
                        Bit = bit,
                        ValueType = field.FieldType,
                    });
                }

                offset += declaredSyncVars.Length;

                var declaredSyncLists = GetDeclaredSyncLists(layerType);
                foreach (var f in declaredSyncLists)
                {
                    Type elementType = f.FieldType.GetGenericArguments()[0];
                    layer.DeclaredSyncLists.Add(new SyncListInfo() { Type = elementType });
                    aggregatedSyncLists.Add(new SyncListInfo() { Type = elementType });
                }

                GenerateComponentClass(path, leafType, layer);

                prevComponent = layer.ComponentClassName;
            }

            List<CommandInfo> commandsInBehaviour = new List<CommandInfo>();
            var commands = leafType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.IsDefined(typeof(CommandAttribute), inherit: true));

            foreach (var method in commands)
            {
                string fullName = method.ToFunctionFullName();
                int hash = fullName.GetStableHashCode();

                commandsInBehaviour.Add(new CommandInfo()
                {
                    FunctionFullName = fullName,
                    Name = method.Name,
                    Parameters = method.GetParameters(),
                    Hash = hash,
                });
            }

            var rpcs = leafType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.IsDefined(typeof(ClientRpcAttribute), inherit: true));
            foreach (var method in rpcs)
            {
                string fullName = method.ToFunctionFullName();
                int hash = fullName.GetStableHashCode();

                ///
            }

            var targetRpcs = leafType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.IsDefined(typeof(TargetRpcAttribute), inherit: true));
            foreach (var method in targetRpcs)
            {
                string fullName = method.ToFunctionFullName();
                int hash = fullName.GetStableHashCode();

                //
            }

            BehaviourInfo behaviourInfo = new BehaviourInfo();
            behaviourInfo.BehaviourType = leafType;
            behaviourInfo.Name = leafType.Name;
            behaviourInfo.Commands = commandsInBehaviour;
            behaviourInfo.SyncLists = aggregatedSyncLists;

            behaviourInfos.Add(behaviourInfo);
        }

        string hierarchy = identity.transform.GetHierarchyPath();

        assetId = identity.assetId;

        sb.AppendLine();
        sb.AppendLine($"namespace SiteLink.API.Networking.Objects;");
        sb.AppendLine();
        sb.AppendLine("//");
        sb.AppendLine($"// Name: {identity.gameObject.name}");
        sb.AppendLine($"// NetworkID: {identity.netId}");
        sb.AppendLine($"// AssetID: {identity.assetId}");
        sb.AppendLine($"// SceneID: {identity.sceneId}");
        sb.AppendLine($"// Path: {hierarchy}");
        sb.AppendLine("//");
        sb.AppendLine($"public class {className} : NetworkObject");
        sb.AppendLine("{");
        sb.AppendLine("    public const uint ObjectAssetId = " + identity.assetId + ";");
        if (identity.sceneId != 0)
            sb.AppendLine("    public const ulong ObjectSceneId = " + identity.sceneId + ";");

        sb.AppendLine();

        if (identity.netId != 0)
            sb.AppendLine("    public override uint NetworkId { get; set; } = " + identity.netId + ";");

        sb.AppendLine("    public override uint AssetId { get; } = ObjectAssetId;");

        if (identity.sceneId != 0)
            sb.AppendLine("    public override ulong SceneId { get; } = ObjectSceneId;");

        for (int i = 0; i < behaviourInfos.Count; i++)
        {
            BehaviourInfo behaviourInfo = behaviourInfos[i];
            sb.AppendLine($"    public {behaviourInfo.ClassName} {behaviourInfo.NormalName} {{ get; }}");
        }

        sb.AppendLine();
        sb.AppendLine($"    public {className}(World world, Session owner = null, uint networkId = 0) : base(world, owner, networkId)");
        sb.AppendLine("    {");
        sb.AppendLine("        //");
        sb.AppendLine($"        Behaviours = new BehaviourComponent[{behaviourInfos.Count}];");

        for (int i = 0; i < behaviourInfos.Count; i++)
        {
            BehaviourInfo behaviourInfo = behaviourInfos[i];

            sb.AppendLine();
            sb.AppendLine($"        {behaviourInfo.NormalName} = new {behaviourInfo.ClassName}(this);");
            sb.AppendLine($"        Behaviours[{i}] = {behaviourInfo.NormalName};");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        File.WriteAllText(targetPath, sb.ToString());
    }

    private static void GenerateComponentClass(string path, Type leafBehaviourType, BehaviourLayerInfo layer)
    {
        string componentsPath = Path.Combine(path, "Components");

        string fileName = layer.BehaviourType.Name + "Component.cs";
        string targetPath = Path.Combine(componentsPath, fileName);

        if (File.Exists(targetPath))
            return;

        StringBuilder sb = new StringBuilder();

        List<string> nameSpaces = new List<string>();

        foreach (var syncVar in layer.DeclaredSyncVars)
        {
            if (!string.IsNullOrEmpty(syncVar.WriterNamespace) && !nameSpaces.Contains(syncVar.WriterNamespace))
                nameSpaces.Add(syncVar.WriterNamespace);

            if (!string.IsNullOrEmpty(syncVar.ValueType.Namespace) && !nameSpaces.Contains(syncVar.ValueType.Namespace))
                nameSpaces.Add(syncVar.ValueType.Namespace);

            if (syncVar.ValueType.IsNested && syncVar.ValueType.DeclaringType != null)
            {
                string staticUsing = $"static {syncVar.ValueType.DeclaringType.FullName!.Replace('+', '.')}";
                if (!nameSpaces.Contains(staticUsing))
                    nameSpaces.Add(staticUsing);
            }
        }

        foreach (var syncList in layer.DeclaredSyncLists)
        {
            if (!string.IsNullOrEmpty(syncList.Type.Namespace) && !nameSpaces.Contains(syncList.Type.Namespace))
                nameSpaces.Add(syncList.Type.Namespace);

            if (syncList.Type.IsNested && syncList.Type.DeclaringType != null)
            {
                string staticUsing = $"static {syncList.Type.DeclaringType.FullName!.Replace('+', '.')}";
                if (!nameSpaces.Contains(staticUsing))
                    nameSpaces.Add(staticUsing);
            }
        }

        foreach (var ns in nameSpaces)
            sb.AppendLine($"using {ns};");

        sb.AppendLine();
        sb.AppendLine($"namespace SiteLink.API.Networking.Components;");
        sb.AppendLine();

        string className = layer.ComponentClassName;
        string baseType = layer.BaseComponentClassName ?? "BehaviourComponent";

        sb.AppendLine($"public class {className} : {baseType}");
        sb.AppendLine("{");

        for (int i = 0; i < layer.DeclaredSyncVars.Count; i++)
        {
            SyncVarInfo syncVar = layer.DeclaredSyncVars[i];
            sb.AppendLine($"    private {syncVar.ValueName} {syncVar.PrivateName};");
            sb.AppendLine();
        }

        for (int i = 0; i < layer.DeclaredSyncVars.Count; i++)
        {
            SyncVarInfo syncVar = layer.DeclaredSyncVars[i];

            sb.AppendLine($"    public {syncVar.ValueName} {syncVar.NormalName}");
            sb.AppendLine("    {");
            sb.AppendLine($"        get => {syncVar.PrivateName};");
            sb.AppendLine("        set");
            sb.AppendLine("        {");
            sb.AppendLine($"            SetSyncVarDirtyBit({syncVar.Bit}UL);");
            sb.AppendLine($"            {syncVar.PrivateName} = value;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        bool isLeafComponent = layer.BehaviourType == leafBehaviourType;
        componentMap.TryGetValue(leafBehaviourType.FullName, out ComponentGenerationInfo info);
        if (!isLeafComponent)
            info = null;

        if (info != null)
        {
            foreach (var prop in info.ExtraProperties)
            {
                sb.AppendLine($"    public {prop.TypeName} {prop.PropertyName} {{ get; set; }}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        if (layer.BaseComponentClassName == null)
        {
            sb.AppendLine($"    public {className}(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Also provide a public convenience ctor for root usage
            sb.AppendLine($"    public {className}(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())");
            sb.AppendLine("    {");
            sb.AppendLine("        //");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        else
        {
            if (isLeafComponent)
            {
                string syncObjectsText = BuildSyncObjectsForLeaf(leafBehaviourType);

                sb.AppendLine($"    public {className}(NetworkObject networkObject) : base(networkObject{syncObjectsText})");
                sb.AppendLine("    {");
                sb.AppendLine("        // subscribe only once is done by root; here we only attach leaf hooks");
                if (info != null)
                {
                    foreach (var hook in info.SerializationHooks)
                        sb.AppendLine($"        this.On{hook.MethodName} += {hook.MethodName};");
                }
                sb.AppendLine("    }");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine($"    public {className}(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)");
                sb.AppendLine("    {");
                sb.AppendLine("        //");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }

        sb.AppendLine($"    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.SerializeSyncVars(writer, forceAll);");
        sb.AppendLine();
        sb.AppendLine("        if (forceAll)");
        sb.AppendLine("        {");

        foreach (var sv in layer.DeclaredSyncVars)
            sb.AppendLine($"            writer.{sv.WriterName}({sv.PrivateName});");

        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteULong(SyncVarDirtyBits);");
        sb.AppendLine();

        foreach (var sv in layer.DeclaredSyncVars)
        {
            sb.AppendLine();
            sb.AppendLine($"        if ((SyncVarDirtyBits & {sv.Bit}UL) != 0UL)");
            sb.AppendLine("        {");
            sb.AppendLine($"            writer.{sv.WriterName}({sv.PrivateName});");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        if (info != null)
        {
            foreach (var hook in info.SerializationHooks)
            {
                sb.AppendLine($"    void {hook.MethodName}(NetworkWriter writer, bool initial)");
                sb.AppendLine("    {");
                sb.AppendLine($"        if ({(hook.RunOnInitialOnly ? "" : "!")}initial)");
                sb.AppendLine("        {");
                foreach (var call in hook.WriteCalls)
                    sb.AppendLine($"            {call}");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");

        File.WriteAllText(targetPath, sb.ToString());
    }

    private static IEnumerable<Type> GetBehaviourChain(Type leaf)
    {
        var stack = new Stack<Type>();
        var t = leaf;

        while (t != null && typeof(NetworkBehaviour).IsAssignableFrom(t) && t != typeof(NetworkBehaviour))
        {
            stack.Push(t);
            t = t.BaseType;
        }

        return stack;
    }

    private static FieldInfo[] GetDeclaredSyncVars(Type t)
    {
        return t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(f => f.IsDefined(typeof(SyncVarAttribute), inherit: false))
                .ToArray();
    }

    private static FieldInfo[] GetDeclaredSyncLists(Type t)
    {
        return t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(f =>
                    f.FieldType.IsGenericType &&
                    f.FieldType.GetGenericTypeDefinition() == typeof(SyncList<>))
                .ToArray();
    }

    private static string BuildSyncObjectsForLeaf(Type leafBehaviourType)
    {
        var chain = GetBehaviourChain(leafBehaviourType).ToList();
        var elementTypes = new List<Type>();

        foreach (var t in chain)
        {
            foreach (var f in GetDeclaredSyncLists(t))
            {
                elementTypes.Add(f.FieldType.GetGenericArguments()[0]);
            }
        }

        if (elementTypes.Count == 0)
            return "";

        var sb = new StringBuilder();
        foreach (var el in elementTypes)
        {
            sb.Append($", new SyncListObject<{el.ToAlias()}>()");
        }
        return sb.ToString();
    }

    private static List<NetworkIdentity> ScanForNetworkIdentities()
    {
        List<NetworkIdentity> networkIdentities = new List<NetworkIdentity>();

        networkIdentities.AddRange(UnityEngine.Object.FindObjectsByType<NetworkIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.sceneId != 0));

        foreach (var prefab in NetworkClient.prefabs)
        {
            if (!prefab.Value.TryGetComponent(out NetworkIdentity identity))
                continue;

            if (!networkIdentities.Contains(identity))
                networkIdentities.Add(identity);
        }

        return networkIdentities;
    }

    private static void RegenerateFolder(string folder)
    {
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);

        Directory.CreateDirectory(folder);
    }
}
