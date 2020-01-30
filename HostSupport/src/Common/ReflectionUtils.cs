using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace DND.EmbedUtils
{
    class ReflectionUtils
    {
        // Default assembly for use with dynamic features (such as type creation for delegates)
        public static AssemblyBuilder DefaultDynamicAssembly { get; } 
            = InitDynamicAssembly();

        public static ModuleBuilder DefaultAssemblyModule { get; private set; }

        // set up default assembly builder with a default module
        private static AssemblyBuilder InitDynamicAssembly()
        {
            var id = Guid.NewGuid().ToString();

            var assembly = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(id), 
                AssemblyBuilderAccess.RunAndCollect
                );

            DefaultAssemblyModule = assembly.DefineDynamicModule(id + ".dll");

            return assembly;
        }
    }


    static class ReflectionExtensions
    {
        

        static ModuleBuilder GetOrAddModule(this AssemblyBuilder builder, string modName)
        {
            var mod = builder.GetModule(modName) as ModuleBuilder;

            if (mod == null)
            {
                mod = builder.DefineDynamicModule(modName);
            }

            return mod;
        }
    }
}
