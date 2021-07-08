namespace BovineLabs.Event.Containers
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

    [DebuggerTypeProxy(typeof(UnsafeArrayReadOnlyDebugView<>))]
    [DebuggerDisplay("Length = {Length}")]
    public unsafe struct UnsafeReadArray<T> : IDisposable
        where T : struct
    {
        [NativeDisableUnsafePtrRestriction]
        private void* m_Buffer;

        private int m_Length;
        private Allocator m_AllocatorLabel;

        internal UnsafeReadArray(void* buffer, int length, Allocator allocator)
        {
            this.m_Buffer = buffer;
            this.m_Length = length;
            this.m_AllocatorLabel = allocator;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if ((IntPtr)this.m_Buffer == IntPtr.Zero)
            {
                throw new ObjectDisposedException("The ReadArray is already disposed.");
            }

            if (this.m_AllocatorLabel == Allocator.Invalid)
            {
                throw new InvalidOperationException("The ReadArray can not be Disposed because it was not allocated with a valid allocator.");
            }

            if (this.m_AllocatorLabel > Allocator.None)
            {
                UnsafeUtility.Free(this.m_Buffer, this.m_AllocatorLabel);
                this.m_AllocatorLabel = Allocator.Invalid;
            }

            this.m_Buffer = null;
            this.m_Length = 0;
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (this.m_AllocatorLabel == Allocator.Invalid)
            {
                throw new InvalidOperationException("The ReadArray can not be Disposed because it was not allocated with a valid allocator.");
            }

            if ((IntPtr)this.m_Buffer == IntPtr.Zero)
            {
                throw new InvalidOperationException("The ReadArray is already disposed.");
            }

            if (this.m_AllocatorLabel > Allocator.None)
            {
                var jobHandle = new DisposeJob
                {
                    Data = new ReadArrayDispose
                    {
                        m_Buffer = this.m_Buffer,
                        m_AllocatorLabel = this.m_AllocatorLabel,
                    },
                }.Schedule(inputDeps);

                this.m_Buffer = null;
                this.m_Length = 0;
                this.m_AllocatorLabel = Allocator.Invalid;
                return jobHandle;
            }

            this.m_Buffer = null;
            this.m_Length = 0;
            return inputDeps;
        }

        public bool IsValid => this.m_Buffer != null;

        public int Length => this.m_Length;

        internal T[] ToArray()
        {
            var dst = new T[this.m_Length];
            var gcHandle = GCHandle.Alloc(dst, GCHandleType.Pinned);
            UnsafeUtility.MemCpy(
                (void*)((IntPtr)(void*)gcHandle.AddrOfPinnedObject()),
                (void*)((IntPtr)this.m_Buffer),
                this.m_Length * UnsafeUtility.SizeOf<T>());
            gcHandle.Free();

            return dst;
        }

        public T this[int index]
        {
            get
            {
                this.CheckElementReadAccess(index);
                return UnsafeUtility.ReadArrayElement<T>(this.m_Buffer, index);
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckElementReadAccess(int index)
        {
            if (index < 0 || index >= this.m_Length)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of range (must be between 0 and {this.m_Length - 1}).");
            }
        }

        internal struct DisposeJob : IJob
        {
            internal ReadArrayDispose Data;

            public void Execute() => this.Data.Dispose();
        }

        internal struct ReadArrayDispose
        {
            [NativeDisableUnsafePtrRestriction]
            internal void* m_Buffer;
            internal Allocator m_AllocatorLabel;

            public void Dispose() => UnsafeUtility.Free(this.m_Buffer, this.m_AllocatorLabel);
        }
    }

    internal sealed class UnsafeArrayReadOnlyDebugView<T>
        where T : unmanaged
    {
        private UnsafeReadArray<T> m_Array;

        public UnsafeArrayReadOnlyDebugView(UnsafeReadArray<T> array) => this.m_Array = array;

        public T[] Items => this.m_Array.ToArray();
    }
}
