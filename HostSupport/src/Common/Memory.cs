using System;
using System.Collections.Generic;
using System.Text;

namespace DND.EmbedUtils
{
    // All this class does is pairing up GCHandle with integers. 
    // Integer handle goes to C functions and back, objects goes to CLR.
    // Even though it doesn't add much to existing GCHandle it is here to cover up for future possibility
    // of dramatic refactoring.
    public sealed class Memory
    {
        private sealed class Handle
        {
            uint _refCount;
            System.Runtime.InteropServices.GCHandle _object;

            public Handle(object obj)
            {
                _object = System.Runtime.InteropServices.GCHandle.Alloc(obj); //, System.Runtime.InteropServices.GCHandleType.Pinned);
                AddRef();
            }

            public void AddRef() 
                => _refCount += 1;

            public bool Release()
            {
                _refCount -= 1;
                if (_refCount == 0)
                {
                    _object.Free();
                    return true;
                }
                return false;
            }

            public object Object { get => _object.Target; }
        }


        // Allocate a new handle every time (does no checks for existing items)
        public static int CreateHandle(object obj)
        {
            var handle = new Handle(obj);
            int newId = _lastId + 1;
            _lastId += 1;
            _storage.Add(newId, handle);
            return newId;
        }


        public static object GetObject(int handle)
        {
            if (_storage.TryGetValue(handle, out Handle h))
                return h.Object;

            return null;
        }


        public static void Release(int handle)
        {
            if (_storage.TryGetValue(handle, out Handle h))
                if (h.Release())
                    _storage.Remove(handle);
        }


        public static int Null { get => 0; }

        // 0 is sentinel value
        private static int _lastId = 1; 
        private static Dictionary<int, Handle> _storage = new Dictionary<int, Handle>();
    }


}
