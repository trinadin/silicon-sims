using FSO.Files.Formats.IFF;
using FSO.SimAntics.JIT.Translation.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FSO.SimAntics.JIT.Runtime
{
    public class AssemblyStore
    {
        private Dictionary<string, SimAnticsModule> FormattedNameToModule = new Dictionary<string, SimAnticsModule>();

        public static bool IsVersionCompatible(uint version) => version == CSTranslator.JITVersion;

        public void InitAOT()
        {
            var modules = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(t => t.GetTypes())
                .Where(t => t.IsClass && t.IsSubclassOf(typeof(SimAnticsModule)));

            foreach (var module in modules)
            {
                var iffName = module.Namespace.Substring(module.Namespace.LastIndexOf('.') + 1);
                var inst = (SimAnticsModule)Activator.CreateInstance(module);
                // Old compiled branches cannot be corrected by the runtime
                // primitive handler. Fall back to interpreting the source IFF.
                if (!IsVersionCompatible(inst.JITVersion)) continue;
                inst.Source = Model.ModuleSource.AOT;
                FormattedNameToModule[iffName] = inst;
            }

            Console.WriteLine(FormattedNameToModule.Count + " Modules Loaded.");
        }

        public SimAnticsModule GetModuleFor(IffFile source)
        {
            if (source.CachedJITModule != null)
            {
                var cached = (SimAnticsModule)source.CachedJITModule;
                if (IsVersionCompatible(cached.JITVersion)) return cached;
                source.CachedJITModule = null;
            }
            var name = CSTranslationContext.FormatName(source.Filename.Substring(0, source.Filename.Length-4));
            SimAnticsModule result;
            if (FormattedNameToModule.TryGetValue(name, out result))
            {
                if (!IsVersionCompatible(result.JITVersion)) return null;
                //TODO: checksum
                if (!result.Inited) result.Init();
                source.CachedJITModule = result;
                return result;
            }
            return null;
        }
    }
}
