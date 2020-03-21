namespace BovineLabs.Event.Containers
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;

    [NativeContainer]
    public unsafe struct NativeThreadStream<T> : IDisposable
        where T : unmanaged
    {
        private UnsafeThreadStream stream;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
        private AtomicSafetyHandle m_Safety;

        [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
        [NativeSetClassTypeToNullOnSchedule]
        private DisposeSentinel m_DisposeSentinel;
#endif

        public NativeThreadStream(Allocator allocator)
        {
            AllocateBlock(out this, allocator);
            this.stream.AllocateForEach(JobsUtility.MaxJobThreadCount);
        }

        public Writer AsWriter()
        {
            return new Writer(ref this);
        }

        static void AllocateBlock(out NativeThreadStream<T> stream, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator <= Allocator.None)
            {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", "allocator");
            }
#endif
            UnsafeThreadStream.AllocateBlock(out stream.stream, allocator);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out stream.m_Safety, out stream.m_DisposeSentinel, 0, allocator);
#endif
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            stream.Dispose();
        }

        [NativeContainer]
        [NativeContainerSupportsMinMaxWriteRestriction]
        public struct Writer
        {
            UnsafeThreadStream.Writer writer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
            [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
            private AtomicSafetyHandle m_Safety;
#endif

            internal Writer(ref NativeThreadStream<T> stream)
            {
                this.writer = stream.stream.AsWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.m_Safety = stream.m_Safety;
#endif
            }

            /// <summary>
            /// Write data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            public void Write(T value)
            {
                ref T dst = ref this.Allocate();
                dst = value;
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            public ref T Allocate()
            {
                CollectionHelper.CheckIsUnmanaged<T>();
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtilityEx.AsRef<T>(this.Allocate(size));
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <param name="size">Size in bytes.</param>
            public byte* Allocate(int size)
            {
                this.AllocateChecks(size);
                return this.writer.Allocate(size);
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local", Justification = "Intentional")]
            private void AllocateChecks(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);

                if (size > UnsafeThreadStreamBlockData.AllocationSize - sizeof(void*))
                {
                    throw new ArgumentException("Allocation size is too large");
                }
#endif
        }
    }
}