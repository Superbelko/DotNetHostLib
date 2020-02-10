using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DND.EmbedUtils
{
    public sealed class Types
    {
        // Simple wrapper for Type.GetType since it doesn't work with .NET core create delegate routine.
        // It is up to you to free it when no longer needed.
        public static IntPtr GetType(string name)
        {
            var ty = Type.GetType(name);
            if (ty != null)
            {
                return GCHandleAlloc(ty);
            }

            return IntPtr.Zero;
        }


        public static IntPtr GetMethod(IntPtr type, string name)
        {
            if (type != IntPtr.Zero)
            {
                var ty = (Type) System.Runtime.InteropServices.GCHandle.FromIntPtr(type).Target;
                if (ty != null)
                {
                    var mi = ty.GetMethod(name);
                    if (mi != null)
                    {
                        return GCHandleAlloc(mi);
                    }
                }
            }
            return IntPtr.Zero;
        }

        public static IntPtr GetMethodWithParams(IntPtr type, string name, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] string[] paramTypes, int paramCount)
        {
            if (type != IntPtr.Zero && paramCount > 0)
            {
                var ty = (Type) System.Runtime.InteropServices.GCHandle.FromIntPtr(type).Target;
                if (ty != null)
                {
                    Type[] types = new Type[paramCount];
                    for(int i = 0; i < paramCount; i++)
                    {
                        var t = Type.GetType(paramTypes[i]);
                        if (t != null)
                            types[i] = t;
                        else
                            return IntPtr.Zero;
                    }

                    var mi = ty.GetMethod(name, types);
                    if (mi != null)
                    {
                        return GCHandleAlloc(mi);
                    }
                }
            }
            return IntPtr.Zero;
        }


        // Activator.CreateInstance(Type) wrapper
        public static IntPtr CreateInstance(IntPtr type)
        {
            if (type != IntPtr.Zero)
            {
                var ty = (Type)System.Runtime.InteropServices.GCHandle.FromIntPtr(type).Target;
                if (ty != null)
                {
                    // NOTE: must be freed up or will leak
                    var gch = System.Runtime.InteropServices.GCHandle.Alloc(Activator.CreateInstance(ty));
                    return System.Runtime.InteropServices.GCHandle.ToIntPtr(gch);
                }
            }

            return IntPtr.Zero;
        }

        // Arrays of primitive types already dealt with by marshaller, 
        // however we also need a way to handle reference types, so this is what it's for
        
        // Create simple 1D array using provided type and length
        public static IntPtr ArrayCreate(IntPtr type, int length)
        {
            if (type != IntPtr.Zero && length > 0)
            {
                var ty = (Type)System.Runtime.InteropServices.GCHandle.FromIntPtr(type).Target;
                if (ty != null)
                {
                    // NOTE: must be freed up or will leak
                    var gch = System.Runtime.InteropServices.GCHandle.Alloc(Array.CreateInstance(ty, length));
                    return System.Runtime.InteropServices.GCHandle.ToIntPtr(gch);
                }
            }

            return IntPtr.Zero;
        }


        // Get array length or -1 on failure
        public static int ArrayLength(IntPtr array)
        {
            if (array != IntPtr.Zero)
            {
                var arr = (Array)System.Runtime.InteropServices.GCHandle.FromIntPtr(array).Target;
                if (arr != null)
                {
                    return arr.GetLength(0);
                }
            }

            return -1;
        }


        public static IntPtr ArrayGetElement(IntPtr array, int indexAt)
        {
            if (array != IntPtr.Zero)
            {
                var arr = (Array)System.Runtime.InteropServices.GCHandle.FromIntPtr(array).Target;
                if (arr != null 
                    && 0 <= indexAt && indexAt < arr.GetLength(0))
                {
                    var item = arr.GetValue(indexAt);
                    return GCHandleAlloc(item);
                }
            }

            return IntPtr.Zero;
        }


        public static void ArraySetElement(IntPtr array, int indexAt, IntPtr val)
        {
            if (array != IntPtr.Zero)
            {
                var arr = (Array)System.Runtime.InteropServices.GCHandle.FromIntPtr(array).Target;
                if (arr != null
                    && 0 <= indexAt && indexAt < arr.GetLength(0))
                {
                    object item = null;

                    if (val != IntPtr.Zero)
                        item = System.Runtime.InteropServices.GCHandle.FromIntPtr(val).Target;

                    arr.SetValue(item, indexAt);
                }
            }
        }


        // Makes GCHandle
        public static IntPtr GCHandleAlloc(object obj)
        {
            if (obj == null)
                return IntPtr.Zero;

            return (IntPtr)System.Runtime.InteropServices.GCHandle.Alloc(obj);
        }


        // Frees GCHandle
        public static void GCHandleFree(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return;

            System.Runtime.InteropServices.GCHandle.FromIntPtr(handle).Free();
        }


        
        // Builds array of types from parameters string specification.
        // for example
        //     int,int,object,string
        // is equivalent for
        //     new[] { typeof(int), typeof(int), typeof(object), typeof(string) }
        //
        // ISSUE:
        //     Type.GetType expects assembly after comma, and comma used in there as delimiter
        //     until it fixed this method is marked private.
        private static System.Type[] ParseTypes(string paramList)
        {
            System.Type[] result = null;

            var params_ = paramList.Split(',');
            result = new System.Type[params_.Length];

            for (int i = 0; i < params_.Length; i++)
            {
                if (BuiltInTypes.TryGetValue(params_[i], out System.Type ty))
                {
                    result[i] = ty;
                    continue;
                }

                result[i] = Type.GetType(params_[i]);

                // type not found and this method should either succeed or return null
                if (result[i] == null)
                    return null;
            }

            return result;
        }


        // Names in this dict reflects CIL, though some aliases added as well
        private static readonly Dictionary<string, System.Type> BuiltInTypes =
        new Dictionary<string, System.Type>()
        {
            { "void", typeof(void) },
            { "bool", typeof(bool) },
            { "char", typeof(char) },
            { "object", typeof(object) },
            { "string", typeof(string) },
            { "unsigned int8", typeof(byte) },
            { "uint8", typeof(byte) },
            { "int8",typeof(sbyte) },
            { "unsigned int16", typeof(ushort) },
            { "uint16", typeof(ushort) },
            { "int16", typeof(short) },
            { "unsigned int32", typeof(uint) },
            { "uint32", typeof(uint) },
            { "int32", typeof(int) },
            { "unsigned int64", typeof(ulong) },
            { "uint64", typeof(ulong) },
            { "int64", typeof(long) },
            { "float32", typeof(float) },
            { "single", typeof(float) },
            { "float64", typeof(double) },
            { "double", typeof(double) },
        };
    }
}
