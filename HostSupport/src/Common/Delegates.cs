using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Linq.Expressions;


namespace DND.EmbedUtils
{
    public class Delegates
    {
        /*
        Problem:
            .NET Core supports only static method, with no reference types, no generics, only intptr[] params and return,
            but we need all of those as callable delegate

        This module tries to solve that problem.
        */

        // Expects methodInfo handle for target method
        // NOTE: must be freed up or will leak
        public static IntPtr CreateForMethod(IntPtr methodInfo)
        {
            if (methodInfo == IntPtr.Zero)
                return IntPtr.Zero;

            var targetMethod = (MethodInfo)System.Runtime.InteropServices.GCHandle.FromIntPtr(methodInfo).Target;
            if (targetMethod == null)
                return IntPtr.Zero;

            var dele = CreateNativeOpaqueDelegate(targetMethod);

            return Types.GCHandleAlloc(dele);
        }


        // Creates direct method delegate (usually not what you want in native code), can be called with DynamicInvoke (slow)
        private static Delegate CreateRegularDelegate(MethodInfo targetMethod)
        {
            var dynAss = ReflectionUtils.DefaultDynamicAssembly;
            var dynMod = ReflectionUtils.DefaultAssemblyModule;
            var dynDel = dynMod.DefineType("MyLittleDelegate", TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoLayout, typeof(System.MulticastDelegate));
            var constructorBuilder = dynDel.DefineConstructor(
                MethodAttributes.RTSpecialName
                | MethodAttributes.HideBySig
                | MethodAttributes.Public,
                CallingConventions.Standard,
                new[] { typeof(object), typeof(System.IntPtr) },
                null, null
                );

            constructorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            // Add call conventions for it

            var attrCtorParams = new[] { typeof(CallingConvention) };
            ConstructorInfo attrCtorInfo = typeof(UnmanagedFunctionPointerAttribute).GetConstructor(attrCtorParams);

            CustomAttributeBuilder attrBuilder = new CustomAttributeBuilder(
                                attrCtorInfo,
                                new object[] { CallingConvention.Cdecl });

            constructorBuilder.SetCustomAttribute(attrBuilder);

            // Grab the parameters of the method

            ParameterInfo[] parameters = targetMethod.GetParameters();

            // need to check the specs about calling conventions, just to ensure there is no other stuff that affects this
            int hiddenThis = targetMethod.IsStatic ? 0 : 1;
            System.Type[] paramTypes = new System.Type[parameters.Length + hiddenThis];

            if (hiddenThis != 0)
            {
                paramTypes[0] = targetMethod.DeclaringType;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                paramTypes[i + hiddenThis] = parameters[i].ParameterType;
            }

            // Define the Invoke method for the delegate
            var methodBuilder = dynDel.DefineMethod("Invoke",
                MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Virtual,
                targetMethod.ReturnType,
                paramTypes
                );

            methodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            var ti = dynDel.CreateTypeInfo();

            var @delegate = System.Delegate.CreateDelegate(
                type: ti,
                method: targetMethod
            );

            return @delegate;
        }

        //
        // private delegate void AddInvoker(List<int> list, int add);
        //
        // ^^^ this delegate results in following IL (note that for .NET Core BeginInvoke/EndInvoke should be omitted)
        //
        // .class nested private auto ansi sealed AddInvoker
        // extends [System.Private.CoreLib]System.MulticastDelegate
        // {
        //     // Methods
        //     .method public hidebysig specialname rtspecialname instance 
        //         void .ctor (
        //             object 'object',
        //             native int 'method'
        //         ) runtime managed 
        //     {
        //     } // end of method AddInvoker::.ctor
        // 
        //     .method public hidebysig newslot virtual instance 
        //         int32 Invoke (
        //             class [System.Private.CoreLib]System.Collections.Generic.List`1<int32> list,
        //             int32 'add'
        //         ) runtime managed 
        //     {
        //     } // end of method AddInvoker::Invoke
        // 
        //     // BeginInvoke(), EndInvoke() ...
        // 
        // } // end of class AddInvoker
        //

        private static Delegate CreateNativeOpaqueDelegate(MethodInfo targetMethod)
        {
            return CreateNativeOpaqueDelegate_Impl(targetMethod);
        }

        public static object getFromPtr(IntPtr handle)
        {
            return GCHandle.FromIntPtr(handle).Target;
        }


        // Creates wrapper that can be called directly from native code (usually with all parameters rewritten as IntPtr and casted to target type)
        private static Delegate CreateNativeOpaqueDelegate_Impl(MethodInfo targetMethod)
        {
            var dynAss = ReflectionUtils.DefaultDynamicAssembly;
            var dynMod = ReflectionUtils.DefaultAssemblyModule;
            var dynDel = dynMod.DefineType("MyLittleDelegate_" + Guid.NewGuid().ToString(), TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoLayout, typeof(System.MulticastDelegate));
            var constructorBuilder = dynDel.DefineConstructor(
                MethodAttributes.RTSpecialName
                | MethodAttributes.HideBySig
                | MethodAttributes.Public,
                CallingConventions.Standard,
                new[] { typeof(object), typeof(System.IntPtr) },
                null, null
                );

            constructorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            // Add call conventions for it

            var attrCtorParams = new[] { typeof(CallingConvention) };
            ConstructorInfo attrCtorInfo = typeof(UnmanagedFunctionPointerAttribute).GetConstructor(attrCtorParams);

            CustomAttributeBuilder attrBuilder = new CustomAttributeBuilder(
                                attrCtorInfo,
                                new object[] { CallingConvention.Cdecl });

            //constructorBuilder.SetCustomAttribute(attrBuilder);

            // Grab the parameters of the method

            ParameterInfo[] parameters = targetMethod.GetParameters();

            // NOTE: need to check the specs about calling conventions, just to ensure there is no other stuff that affects this
            int hiddenThis = targetMethod.IsStatic ? 0 : 1;
            System.Type[] paramTypes = new System.Type[parameters.Length + hiddenThis];


            // make up function parameters
            if (hiddenThis != 0)
                paramTypes[0] = typeof(IntPtr);
            for (int i = 0; i < parameters.Length; i++)
            {
                // TODO: better, recursive struct handling, as any reference type will explode on call
                paramTypes[i+hiddenThis] = MapToNativeType(parameters[i].ParameterType);
            }

            
            // Define the Invoke method for the delegate
            var methodBuilder = dynDel.DefineMethod("Invoke",
                MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Virtual,
                typeof(IntPtr),
                paramTypes
                );

            methodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            // Define wrapper method for the delegate
            var wrapper = dynDel.DefineMethod("GenCode",
                MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Static,
                typeof(IntPtr),
                paramTypes
                );

            wrapper.SetImplementationFlags(MethodImplAttributes.Managed);
            wrapper.SetCustomAttribute(attrBuilder);


            var il = wrapper.GetILGenerator();

            // push 'this' to stack
            if (!targetMethod.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);

                // retrieve actual object from handle
                var thisType = targetMethod.DeclaringType;
                if (thisType.IsClass || thisType.IsArray)
                {
                    il.Emit(OpCodes.Call, typeof(Delegates).GetMethod(nameof(getFromPtr)));
                    // not necessary, cast to callee type
                    //il.Emit(OpCodes.Castclass, targetMethod.DeclaringType); 
                }

            }

            // push parameters
            for (int i = 0; i < parameters.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, i + hiddenThis);

                if (nativeTypes.Contains(parameters[i].ParameterType))
                {
                    // do nothing. handled by runtime
                }
                else if (parameters[i].ParameterType.IsClass)
                    il.Emit(OpCodes.Call, typeof(Delegates).GetMethod(nameof(getFromPtr)));
                else
                    throw new NotImplementedException();
            }

            // NOTE: even though it says callvirt it also can call regular methods as well
            // the main difference is extra null check done by callvirt
            il.Emit((targetMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt), targetMethod);

            // add some space to allow rewriting, often done by C# compiler
            il.Emit(OpCodes.Nop); 

            if (targetMethod.ReturnType == typeof(void))
                il.Emit(OpCodes.Ldsfld, typeof(IntPtr).GetField("Zero"));
            else if (!nativeTypes.Contains(targetMethod.ReturnType)) // wrap objects types
                il.Emit(OpCodes.Call, typeof(GCHandle).GetMethod(nameof(GCHandle.Alloc), new Type[] {typeof(Type)}));

            il.Emit(OpCodes.Ret);

            

            var ti = dynDel.CreateTypeInfo();

            var @delegate = System.Delegate.CreateDelegate(ti, ti.GetMethod("GenCode"));

            return @delegate;
        }


        public static IntPtr ToFunctionPointer(IntPtr delegateHandle)
        {
            if (delegateHandle == IntPtr.Zero)
                return IntPtr.Zero;

            var del = GCHandle.FromIntPtr(delegateHandle).Target as MulticastDelegate;
            if (del == null)
                return IntPtr.Zero;

            return Marshal.GetFunctionPointerForDelegate(del);
        }


        public static IntPtr DynamicInvoke(
            IntPtr delegateHandle, // 0
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]IntPtr[] types,  // 1
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]IntPtr[] args,  // 2
            int numArgs) // 3 (size)
        {
            if (delegateHandle == IntPtr.Zero || numArgs <= 0)
                return IntPtr.Zero;

            var del = GCHandle.FromIntPtr(delegateHandle).Target as MulticastDelegate;
            if (del == null)
                return IntPtr.Zero;

            object[] arg = new object[numArgs];
            for (int i = 0; i < numArgs; i++)
            {
                var t = GCHandle.FromIntPtr(types[i]);
                if (t.Target == null)
                    return IntPtr.Zero;

                var ty = (Type)t.Target;


                object value;
                var h = GCHandle.FromIntPtr(args[i]);
                if (h != null && !ty.IsValueType)
                {
                    value = Convert.ChangeType(h.Target, ty);
                    arg[i] = value;
                }
                else
                {
                    if (ty == typeof(int))
                        arg[i] = args[i].ToInt32();
                    else
                        arg[i] = args[i];
                }
            }

            var res = del.DynamicInvoke(arg);

            if (res != null)
            {
                return (IntPtr)GCHandle.Alloc(res);
            }

            return IntPtr.Zero;
        }


        public static IntPtr DynamicInvokeAuto(IntPtr delegateHandle, IntPtr[] args, int numArgs)
        {
            if (delegateHandle == IntPtr.Zero || numArgs <= 0)
                return IntPtr.Zero;

            var del = GCHandle.FromIntPtr(delegateHandle).Target as MulticastDelegate;
            if (del == null)
                return IntPtr.Zero;

            object[] arg = new object[numArgs];
            for (int i = 0; i < numArgs; i++)
            {
                var h = GCHandle.FromIntPtr(args[i]);
                if (h.Target == null)
                    return IntPtr.Zero;

                arg[i] = h.Target;
            }

            var res = del.DynamicInvoke(arg);

            if (res != null)
            {
                return (IntPtr)GCHandle.Alloc(res);
            }

            return IntPtr.Zero;
        }


        public static Type MapToNativeType(Type t)
        {
            if (nativeTypes.Contains(t))
                return t;

            if (t.IsValueType && !t.IsGenericType)
            {
                if (t.GetFields().All(x => nativeTypes.Contains(x.FieldType)))
                    return t;
            }

            return typeof(IntPtr);
        }

        private static List<Type> nativeTypes = new List<Type> {
            typeof(string), typeof(int), typeof(uint), typeof(char), typeof(byte), typeof(sbyte),
            typeof(short), typeof(ushort), typeof(long), typeof(ulong), typeof(float), typeof(double)
        };
    }

    static class EmitExtensions
    {
        /// Cached GCHandle.Alloc() method
        readonly static MethodInfo gchAlloc = typeof(GCHandle).GetMethod("Alloc", new[] { typeof(IntPtr) });

        /// Cached GCHandle.ToIntPtr() method
        readonly static MethodInfo gchToIntPtr = typeof(GCHandle).GetMethod("ToIntPtr");

        /// Takes top stack element and does GCHandle.Alloc resulting in GCHandle on the top of the stack
        public static void EmitGCHandleAlloc(this ILGenerator il)
        {
            il.Emit(OpCodes.Call, gchAlloc);
        }

        public static void EmitGCHandleCastToIntPtr(this ILGenerator il)
        {
            il.Emit(OpCodes.Call, gchToIntPtr);
        }
    }
}
