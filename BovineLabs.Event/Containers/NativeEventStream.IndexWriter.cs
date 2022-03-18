namespace BovineLabs.Event.Containers
{
    using System;
    using System.Diagnostics;
    using Unity.Assertions;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct NativeEventStream
    {
        /// <summary> The writer instance. </summary> // TODO
        [NativeContainer]
        [NativeContainerSupportsMinMaxWriteRestriction]
        public struct IndexWriter : IStreamWriter
        {
            private UnsafeEventStream.IndexWriter writer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private AtomicSafetyHandle m_Safety;
#pragma warning disable CS0414 // warning CS0414: The field 'NativeEventStream.Writer.m_Length' is assigned but its value is never used
            private int m_Length;
#pragma warning restore CS0414
            private int m_MinIndex;
            private int m_MaxIndex;

            [NativeDisableUnsafePtrRestriction]
            private void* m_PassByRefCheck;
#endif

            internal IndexWriter(ref NativeEventStream stream)
            {
                this.writer = stream.stream.AsIndexWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.m_Safety = stream.m_Safety;
                this.m_Length = int.MaxValue;
                this.m_MinIndex = int.MinValue;
                this.m_MaxIndex = int.MinValue;
                this.m_PassByRefCheck = null;
#endif
            }

            /// <summary> Gets the number of streams the container can use. </summary>
            public int ForEachCount => this.writer.ForEachCount;

            /// <summary> Begin reading data at the iteration index. </summary>
            /// <param name="foreachIndex"> The index to work on. </param>
            /// <remarks><para> BeginForEachIndex must always be called balanced by a EndForEachIndex. </para></remarks>
            public void BeginForEachIndex(int foreachIndex)
            {
                this.CheckBeginForEachIndex(foreachIndex);
                this.writer.BeginForEachIndex(foreachIndex);
            }

            /// <summary> Ensures that all data has been read for the active iteration index. </summary>
            /// <remarks><para> EndForEachIndex must always be called balanced by a BeginForEachIndex. </para></remarks>
            public void EndForEachIndex()
            {
                this.CheckEndForEachIndex();
                this.writer.EndForEachIndex();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.writer.m_ForeachIndex = int.MinValue;
#endif
            }

            /// <summary> Write data. </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <param name="value"> The data to write. </param>
            public void Write<T>(T value)
                where T : unmanaged
            {
                ref T dst = ref this.Allocate<T>();
                dst = value;
            }

            /// <summary> Allocate space for data. </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <returns> Reference for the allocated space. </returns>
            public ref T Allocate<T>()
                where T : unmanaged
            {
                CollectionHelper.CheckIsUnmanaged<T>();
                int size = UnsafeUtility.SizeOf<T>();
#if UNITY_COLLECTIONS_0_14_OR_NEWER
                return ref UnsafeUtility.AsRef<T>(this.Allocate(size));
#else
                return ref UnsafeUtilityEx.AsRef<T>(this.Allocate(size));
#endif
            }

            /// <summary> Allocate space for data. </summary>
            /// <param name="size">Size in bytes.</param>
            /// <returns> Reference for the allocated space. </returns>
            public byte* Allocate(int size)
            {
                this.CheckAllocateSize(size);
                return this.writer.Allocate(size);
            }

            internal void PatchMinMaxRange(int foreEachIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.m_MinIndex = foreEachIndex;
                this.m_MaxIndex = foreEachIndex;
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckBeginForEachIndex(int foreachIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);

                if (this.m_PassByRefCheck == null)
                {
                    this.m_PassByRefCheck = UnsafeUtility.AddressOf(ref this);
                }

                if (foreachIndex < this.m_MinIndex || foreachIndex > this.m_MaxIndex)
                {
                    // When the code is not running through the job system no ParallelForRange patching will occur
                    // We can't grab m_BlockStream->RangeCount on creation of the writer because the RangeCount can be initialized
                    // in a job after creation of the writer
                    if (this.m_MinIndex == int.MinValue && this.m_MaxIndex == int.MinValue)
                    {
                        this.m_MinIndex = 0;
                        this.m_MaxIndex = this.writer.m_BlockStream->RangeCount - 1;
                    }

                    if (foreachIndex < this.m_MinIndex || foreachIndex > this.m_MaxIndex)
                    {
                        throw new ArgumentException($"Index {foreachIndex} is out of restricted IJobParallelFor range [{this.m_MinIndex}...{this.m_MaxIndex}] in NativeEventStream.");
                    }
                }

                if (this.writer.m_ForeachIndex != int.MinValue)
                {
                    throw new ArgumentException($"BeginForEachIndex must always be balanced by a EndForEachIndex call");
                }

                if (this.writer.m_BlockStream->Ranges[foreachIndex].ElementCount != 0)
                {
                    throw new ArgumentException($"BeginForEachIndex can only be called once for the same index ({foreachIndex}).");
                }

                Assert.IsTrue(foreachIndex >= 0 && foreachIndex < this.writer.m_BlockStream->RangeCount);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckEndForEachIndex()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);

                if (this.writer.m_ForeachIndex == int.MinValue)
                {
                    throw new System.ArgumentException("EndForEachIndex must always be called balanced by a BeginForEachIndex or AppendForEachIndex call");
                }
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckAllocateSize(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);

                if (this.m_PassByRefCheck != UnsafeUtility.AddressOf(ref this))
                {
                    throw new ArgumentException("NativeEventStream.Writer must be passed by ref once it is in use");
                }

                if (this.writer.m_ForeachIndex == int.MinValue)
                {
                    throw new ArgumentException("Allocate must be called within BeginForEachIndex / EndForEachIndex");
                }

                if (size > UnsafeEventStreamBlockData.AllocationSize - sizeof(void*))
                {
                    throw new ArgumentException("Allocation size is too large");
                }
#endif
            }
        }
    }
}
