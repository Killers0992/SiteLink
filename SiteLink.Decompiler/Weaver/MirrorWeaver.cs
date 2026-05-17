using Mirror;
using Mono.CecilX;
using Mono.CecilX.Cil;
using Mono.CecilX.Rocks;
using System.Diagnostics;

namespace SiteLink.Decompiler.Weaver
{
    public class MirrorWeaver
    {
        TypeDefinition GeneratedCodeClass;

        public const string GeneratedCodeClassName = "GeneratedNetworkCode";
        public const string GeneratedCodeNamespace = "Mirror";

        public const string MirrorAssemblyName = "Mirror";

        WeaverTypes weaverTypes; 
        AssemblyDefinition CurrentAssembly;

        Writers writers;
        Readers readers;

        bool WeavingFailed;

        public Logger Log;

        public MirrorWeaver()
        {
        }

        public bool Weave(AssemblyDefinition assembly, IAssemblyResolver resolver, out bool modified)
        {
            WeavingFailed = false;
            modified = false;
            try
            {
                CurrentAssembly = assembly;

                // fix "No writer found for ..." error
                // https://github.com/vis2k/Mirror/issues/2579
                // -> when restarting Unity, weaver would try to weave a DLL
                //    again
                // -> resulting in two GeneratedNetworkCode classes (see ILSpy)
                // -> the second one wouldn't have all the writer types setup
                if (CurrentAssembly.MainModule.ContainsClass(GeneratedCodeNamespace, GeneratedCodeClassName))
                {
                    //Log.Warning($"Weaver: skipping {CurrentAssembly.Name} because already weaved");
                    return true;
                }

                weaverTypes = new WeaverTypes(CurrentAssembly, Log, ref WeavingFailed);

                // weaverTypes are needed for CreateGeneratedCodeClass
                CreateGeneratedCodeClass();

                writers = new Writers(CurrentAssembly, weaverTypes, GeneratedCodeClass, Log);
                readers = new Readers(CurrentAssembly, weaverTypes, GeneratedCodeClass, Log);

                Stopwatch rwstopwatch = Stopwatch.StartNew();
                // Need to track modified from ReaderWriterProcessor too because it could find custom read/write functions or create functions for NetworkMessages
                modified = ReaderWriterProcessor.Process(CurrentAssembly, resolver, Log, writers, readers, ref WeavingFailed);
                rwstopwatch.Stop();
                Console.WriteLine($"Find all reader and writers took {rwstopwatch.ElapsedMilliseconds} milliseconds");

                ModuleDefinition moduleDefinition = CurrentAssembly.MainModule;
                Console.WriteLine($"Script Module: {moduleDefinition.Name}");

                modified |= WeaveModule(moduleDefinition);

                if (WeavingFailed)
                {
                    return false;
                }

                if (modified)
                {
                    moduleDefinition.Types.Add(GeneratedCodeClass);

                    ReaderWriterProcessor.InitializeReaderAndWriters(CurrentAssembly, weaverTypes, writers, readers, GeneratedCodeClass);
                }

                // if weaving succeeded, switch on the Weaver Fuse in Mirror.dll
                if (CurrentAssembly.Name.Name == MirrorAssemblyName)
                {
                    ToggleWeaverFuse();
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Exception :{e}");
                WeavingFailed = true;
                return false;
            }
        }

        void CreateGeneratedCodeClass()
        {
            // create "Mirror.GeneratedNetworkCode" class which holds all
            // Readers<T> and Writers<T>
            GeneratedCodeClass = new TypeDefinition(GeneratedCodeNamespace, GeneratedCodeClassName,
                TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.Abstract | TypeAttributes.Sealed,
                weaverTypes.Import<object>());
        }

        bool WeaveModule(ModuleDefinition moduleDefinition)
        {
            bool modified = false;

            Stopwatch watch = Stopwatch.StartNew();
            watch.Start();

            foreach (TypeDefinition td in moduleDefinition.GetAllTypes())
            {
                if (td.IsClass && td.BaseType.CanBeResolved())
                {
                    //modified |= WeaveNetworkBehavior(td);
                    //modified |= ServerClientAttributeProcessor.Process(weaverTypes, Log, td, ref WeavingFailed);
                }
            }

            watch.Stop();
            Console.WriteLine($"Weave behaviours and messages took {watch.ElapsedMilliseconds} milliseconds");

            return modified;
        }

        void ToggleWeaverFuse()
        {
            MethodDefinition func = weaverTypes.weaverFuseMethod.Resolve();

            ILProcessor worker = func.Body.GetILProcessor();
            func.Body.Instructions[0] = worker.Create(OpCodes.Ldc_I4_1);
        }
    }
}
