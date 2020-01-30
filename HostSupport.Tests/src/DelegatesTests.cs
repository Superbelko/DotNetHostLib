using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace DND.EmbedUtils.Tests
{
    public class Delegates
    {
        [Fact]
        void CreateForMethod_Works()
        {
            var list = new List<int>();
            var listHandle = DND.EmbedUtils.Types.GCHandleAlloc(list);

            var mi = typeof(List<int>).GetMethod("Add");
            var mihandle = GCHandle.Alloc(mi);

            var delhandle = EmbedUtils.Delegates.CreateForMethod(GCHandle.ToIntPtr(mihandle));
            var fptr = EmbedUtils.Delegates.ToFunctionPointer(delhandle);
            var dele = Marshal.GetDelegateForFunctionPointer<MulticastDelegate>(fptr);


            // unfortunately because delegate type is not known ahead and is created at runtime there is no way to create static signature 
            dele.DynamicInvoke(listHandle, 42); 

            Assert.True(list[0] == 42);
        }
    }
}
