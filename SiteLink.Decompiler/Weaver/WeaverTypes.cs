using Mirror;
using Mono.CecilX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.Decompiler.Weaver
{
    public class WeaverTypes
    {
        public MethodReference ScriptableObjectCreateInstanceMethod;

        public MethodReference NetworkBehaviourIsClientReference;
        public MethodReference NetworkBehaviourIsServerReference;
        public FieldReference NetworkBehaviourDirtyBitsReference;
        public MethodReference NetworkBehaviourConnectionToClientReference;
        public MethodReference GetWriterReference;
        public MethodReference ReturnWriterReference;

        public MethodReference NetworkServerLocalConnectionReference;
        public MethodReference NetworkClientConnectionReference;

        public MethodReference RemoteCallDelegateConstructor;

        public MethodReference NetworkServerGetActive;
        public MethodReference NetworkClientGetActive;

        // custom attribute types
        public MethodReference InitSyncObjectReference;

        // array segment
        public MethodReference ArraySegmentConstructorReference;

        // Action<T,T> for SyncVar Hooks
        public MethodReference ActionT_T;

        // syncvar
        public MethodReference generatedSyncVarSetter;
        public MethodReference generatedSyncVarSetter_GameObject;
        public MethodReference generatedSyncVarSetter_NetworkIdentity;
        public MethodReference generatedSyncVarSetter_NetworkBehaviour_T;
        public MethodReference generatedSyncVarDeserialize;
        public MethodReference generatedSyncVarDeserialize_GameObject;
        public MethodReference generatedSyncVarDeserialize_NetworkIdentity;
        public MethodReference generatedSyncVarDeserialize_NetworkBehaviour_T;
        public MethodReference getSyncVarGameObjectReference;
        public MethodReference getSyncVarNetworkIdentityReference;
        public MethodReference getSyncVarNetworkBehaviourReference;
        public MethodReference registerCommandReference;
        public MethodReference registerRpcReference;
        public MethodReference getTypeFromHandleReference;
        public MethodReference logErrorReference;
        public MethodReference logWarningReference;
        public MethodReference sendCommandInternal;
        public MethodReference sendRpcInternal;
        public MethodReference sendTargetRpcInternal;

        public MethodReference readNetworkBehaviourGeneric;

        public TypeReference weaverFuseType;
        public MethodReference weaverFuseMethod;

        // attributes
        public TypeDefinition initializeOnLoadMethodAttribute;
        public TypeDefinition runtimeInitializeOnLoadMethodAttribute;

        AssemblyDefinition assembly;

        public TypeReference Import<T>() => Import(typeof(T));

        public TypeReference Import(Type t) => assembly.MainModule.ImportReference(t);

        // constructor resolves the types and stores them in fields
        public WeaverTypes(AssemblyDefinition assembly, Logger Log, ref bool WeavingFailed)
        {
            // system types
            this.assembly = assembly;

            TypeReference ArraySegmentType = Import(typeof(ArraySegment<>));
            ArraySegmentConstructorReference = Resolvers.ResolveMethod(ArraySegmentType, assembly, Log, ".ctor", ref WeavingFailed);

            TypeReference ActionType = Import(typeof(Action<,>));
            ActionT_T = Resolvers.ResolveMethod(ActionType, assembly, Log, ".ctor", ref WeavingFailed);

            weaverFuseType = Import(typeof(WeaverFuse));
            weaverFuseMethod = Resolvers.ResolveMethod(weaverFuseType, assembly, Log, "Weaved", ref WeavingFailed);

            TypeReference NetworkServerType = Import(typeof(NetworkServer));
            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, assembly, Log, "get_active", ref WeavingFailed);
            NetworkServerLocalConnectionReference = Resolvers.ResolveMethod(NetworkServerType, assembly, Log, "get_localConnection", ref WeavingFailed);

            TypeReference NetworkClientType = Import(typeof(NetworkClient));
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, assembly, Log, "get_active", ref WeavingFailed);
            NetworkClientConnectionReference = Resolvers.ResolveMethod(NetworkClientType, assembly, Log, "get_connection", ref WeavingFailed);

            TypeReference NetworkBehaviourType = Import<NetworkBehaviour>();

            NetworkBehaviourIsClientReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "get_isClient", ref WeavingFailed);
            NetworkBehaviourIsServerReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "get_isServer", ref WeavingFailed);

            NetworkBehaviourDirtyBitsReference = Resolvers.ResolveField(NetworkBehaviourType, assembly, "syncVarDirtyBits", ref WeavingFailed);

            NetworkBehaviourConnectionToClientReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "get_connectionToClient", ref WeavingFailed);

            generatedSyncVarSetter = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GeneratedSyncVarSetter", ref WeavingFailed);
            generatedSyncVarSetter_GameObject = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GeneratedSyncVarSetter_GameObject", ref WeavingFailed);
            generatedSyncVarSetter_NetworkIdentity = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GeneratedSyncVarSetter_NetworkIdentity", ref WeavingFailed);
            generatedSyncVarSetter_NetworkBehaviour_T = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GeneratedSyncVarSetter_NetworkBehaviour", ref WeavingFailed);

            generatedSyncVarDeserialize_GameObject = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GeneratedSyncVarDeserialize_GameObject", ref WeavingFailed);
            generatedSyncVarDeserialize = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GeneratedSyncVarDeserialize", ref WeavingFailed);
            generatedSyncVarDeserialize_NetworkIdentity = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GeneratedSyncVarDeserialize_NetworkIdentity", ref WeavingFailed);
            generatedSyncVarDeserialize_NetworkBehaviour_T = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GeneratedSyncVarDeserialize_NetworkBehaviour", ref WeavingFailed);

            getSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GetSyncVarGameObject", ref WeavingFailed);
            getSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GetSyncVarNetworkIdentity", ref WeavingFailed);
            getSyncVarNetworkBehaviourReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GetSyncVarNetworkBehaviour", ref WeavingFailed);

            sendCommandInternal = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SendCommandInternal", ref WeavingFailed);
            sendRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SendRPCInternal", ref WeavingFailed);
            sendTargetRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SendTargetRPCInternal", ref WeavingFailed);

            InitSyncObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "InitSyncObject", ref WeavingFailed);

            TypeReference RemoteProcedureCallsType = Import(typeof(Mirror.RemoteCalls.RemoteProcedureCalls));
            registerCommandReference = Resolvers.ResolveMethod(RemoteProcedureCallsType, assembly, Log, "RegisterCommand", ref WeavingFailed);
            registerRpcReference = Resolvers.ResolveMethod(RemoteProcedureCallsType, assembly, Log, "RegisterRpc", ref WeavingFailed);

            TypeReference RemoteCallDelegateType = Import<Mirror.RemoteCalls.RemoteCallDelegate>();
            RemoteCallDelegateConstructor = Resolvers.ResolveMethod(RemoteCallDelegateType, assembly, Log, ".ctor", ref WeavingFailed);

            TypeReference typeType = Import(typeof(Type));
            getTypeFromHandleReference = Resolvers.ResolveMethod(typeType, assembly, Log, "GetTypeFromHandle", ref WeavingFailed);

            TypeReference NetworkWriterPoolType = Import(typeof(NetworkWriterPool));
            GetWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, assembly, Log, "Get", ref WeavingFailed);
            ReturnWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, assembly, Log, "Return", ref WeavingFailed);

            TypeReference readerExtensions = Import(typeof(NetworkReaderExtensions));
            readNetworkBehaviourGeneric = Resolvers.ResolveMethod(readerExtensions, assembly, (md =>
            {
                return md.Name == nameof(NetworkReaderExtensions.ReadNetworkBehaviour) &&
                       md.HasGenericParameters;
            }),
            ref WeavingFailed);
        }
    }
}
