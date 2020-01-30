using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

using Mono.Cecil;

namespace DND.EmbedUtils
{

    public struct ModuleInfo
    {
        string name;
        int numTypes;


        public ModuleInfo(ModuleDefinition module)
        {
            name = module.Name;
            numTypes = module.Types.Count;
        }
    }


    public struct TypeInfo
    {
        string fullName;

        public TypeInfo(TypeDefinition type)
        {
            fullName = type.FullName;
        }
    }


    public sealed class AssemblyUtils
    {
        // Enumerate callback, return true to abort enumeration
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool ModuleEnumerate(ModuleInfo moduleInfo);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool TypeEnumerate(TypeInfo typeInfo);


        public static int ReadAssembly(string path)
        {
            if (!System.IO.File.Exists(path))
                return Memory.Null;

            var assembly = AssemblyDefinition.ReadAssembly(path);

            if (assembly == null)
                return Memory.Null;

            return Memory.CreateHandle(assembly);
        }


        public static void CloseAssembly(int assemblyHandle)
        {
            var assembly = AssemblyFromHandle(assemblyHandle);
            if (assembly != null)
                assembly.Dispose();
        }


        public static void EnumerateModules(int assemblyHandle, IntPtr fn)
        {
            var assembly = AssemblyFromHandle(assemblyHandle);

            if (assembly == null || fn == IntPtr.Zero)
                return;

            var dele = Marshal.GetDelegateForFunctionPointer<ModuleEnumerate>(fn);
            if (dele == null)
                return;

            foreach(var mod in assembly.Modules)
            {
                if (!dele(new ModuleInfo(mod)))
                    return;
            }
        }


        public static void EnumerateModuleTypes(int moduleHandle, IntPtr fn)
        {
            var module = ModuleFromHandle(moduleHandle);

            if (module == null || fn == IntPtr.Zero)
                return;

            var dele = Marshal.GetDelegateForFunctionPointer<TypeEnumerate>(fn);
            if (dele == null)
                return;


            foreach (var ty in module.Types)
            {
                try
                {
                    EnumerateTypesImpl(ty, dele);
                }
                catch (System.OperationCanceledException)
                {

                }
            }
        }

        private static void EnumerateTypesImpl(TypeDefinition type, TypeEnumerate callback)
        {
            if (!callback(new TypeInfo(type)))
                throw new OperationCanceledException();
            if (type.HasNestedTypes)
            {
                foreach (var n in type.NestedTypes)
                {
                    EnumerateTypesImpl(n, callback);
                }
            }
        }


        public static int GetModuleCount(int assemblyHandle)
        {
            var assembly = AssemblyFromHandle(assemblyHandle);

            if (assembly == null)
                return -1;

            return assembly.Modules.Count;
        }


        public static int GetModule(int assemblyHandle, int index)
        {
            var assembly = AssemblyFromHandle(assemblyHandle);

            if (assembly == null)
                return Memory.Null;

            if (index < 0 || assembly.Modules.Count < index)
                return Memory.Null;

            return Memory.CreateHandle(assembly.Modules[index]);
        }


        public static int GetTypesCount(int moduleHandle)
        {
            var module = ModuleFromHandle(moduleHandle);

            if (module == null)
                return -1;

            return module.Types.Count;
        }


        // Convenience helpers

        private static AssemblyDefinition AssemblyFromHandle(int handle)
            => Memory.GetObject(handle) as AssemblyDefinition;

        private static ModuleDefinition ModuleFromHandle(int handle)
            => Memory.GetObject(handle) as ModuleDefinition;
    }
}
