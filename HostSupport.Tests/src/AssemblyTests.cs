using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Linq;
using Xunit;

using DND.EmbedUtils;

using System.Runtime.InteropServices;
using System.Reflection.Emit;

namespace DND.EmbedUtils.Tests
{
    public class Assemblies
    {
        [Fact]
        void ReadAssembly_NotNull()
        {
            var path = typeof(void).Assembly.Location;
            var assemblyHandle = AssemblyUtils.ReadAssembly(path);
            AssemblyUtils.CloseAssembly(assemblyHandle); // we don't actually need it, just close
            Assert.True(assemblyHandle != Memory.Null);
        }


        [Fact]
        void ReadAssembly_CanEnumerateModules()
        {
            _moduleEnumCallbackSucceed = false;
            var path = typeof(void).Assembly.Location;
            var assemblyHandle = AssemblyUtils.ReadAssembly(path);

            var dele = Delegate.CreateDelegate(
                typeof(AssemblyUtils.ModuleEnumerate), 
                typeof(DND.EmbedUtils.Tests.Assemblies).GetMethod(nameof(ModuleEnumerateCallback), BindingFlags.Static | BindingFlags.NonPublic)
            );
            var cb = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(dele);

            AssemblyUtils.EnumerateModules(assemblyHandle, cb);

            AssemblyUtils.CloseAssembly(assemblyHandle);

            Assert.True(_moduleEnumCallbackSucceed);
        }

        static private bool _moduleEnumCallbackSucceed = false;

        static private bool ModuleEnumerateCallback(EmbedUtils.ModuleInfo modInfo)
        {
            _moduleEnumCallbackSucceed = true;
            return true;
        }

        static private bool TypeEnumerateCallback(EmbedUtils.TypeInfo typeInfo)
        {
            return true;
        }
    }
}
