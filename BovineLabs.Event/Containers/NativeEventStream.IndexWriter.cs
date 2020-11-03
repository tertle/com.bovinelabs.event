namespace BovineLabs.Event.Containers
{
    using System;
    using System.Diagnostics;
    using Unity.Assertions;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct NativeEventStream
    {
        /// <summary>
        /// </summary>
        [NativeContainer]
        [NativeContainerSupportsMinMaxWriteRestriction]
        public struct IndexWriter : IStreamWriter
        {
            UnsafeEventStream.IndexWriter m_Writer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle m_Safety;
#pragma warning disable CS0414 // warning CS0414: The field 'NativeEventStream.Writer.m_Length' is assigned but its value is never used
            int m_Length;
#pragma warning restore CS0414
            int m_MinIndex;
            int m_MaxIndex;

            [NativeDisableUnsafePtrRestriction]
            void* m_PassByRefCheck;
#endif

            internal IndexWriter(ref NativeEventStream stream)
            {
                m_Writer = stream.stream.AsIndexWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = stream.m_Safety;
                m_Length = int.MaxValue;
                m_MinIndex = int.MinValue;
                m_MaxIndex = int.MinValue;
                m_PassByRefCheck = null;
#endif
            }

            /// <summary>
            ///
            /// </summary>
            public int ForEachCount => m_Writer.ForEachCount;

            /// <summary>
            ///
            /// </summary>
            /// <param name="foreEachIndex"></param>
            public void PatchMinMaxRange(int foreEachIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_MinIndex = foreEachIndex;
                m_MaxIndex = foreEachIndex;
#endif
            }

            /// <summary>
            /// Begin reading data at the iteration index.
            /// </summary>
            /// <param name="foreachIndex"></param>
            /// <remarks>BeginForEachIndex must always be called balanced by a EndForEachIndex.</remarks>
            public void BeginForEachIndex(int foreachIndex)
            {
                //@TODO: Check that no one writes to the same for each index multiple times...
                CheckBeginForEachIndex(foreachIndex);
                m_Writer.BeginForEachIndex(foreachIndex);
            }

            /// <summary>
            /// Ensures that all data has been read for the active iteration index.
            /// </summary>
            /// <remarks>EndForEachIndex must always be called balanced by a BeginForEachIndex.</remarks>
            public void EndForEachIndex()
            {
                CheckEndForEachIndex();
                m_Writer.EndForEachIndex();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Writer.m_ForeachIndex = int.MinValue;
#endif
            }

            /// <summary>
            /// Write data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <param name="value"></param>
            public void Write<T>(T value) where T : struct
            {
                ref T dst = ref Allocate<T>();
                dst = value;
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <returns></returns>
            public ref T Allocate<T>() where T : struct
            {
                CollectionHelper.CheckIsUnmanaged<T>();
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtility.AsRef<T>(Allocate(size));
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <param name="size">Size in bytes.</param>
            /// <returns></returns>
            public byte* Allocate(int size)
            {
                CheckAllocateSize(size);
                return m_Writer.Allocate(size);
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckBeginForEachIndex(int foreachIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);

                if (m_PassByRefCheck == null)
                {
                    m_PassByRefCheck = UnsafeUtility.AddressOf(ref this);
                }

                if (foreachIndex < m_MinIndex || foreachIndex > m_MaxIndex)
                {
                    // When the code is not running through the job system no ParallelForRange patching will occur
                    // We can't grab m_BlockStream->RangeCount on creation of the writer because the RangeCount can be initialized
                    // in a job after creation of the writer
                    if (m_MinIndex == int.MinValue && m_MaxIndex == int.MinValue)
                    {
                        m_MinIndex = 0;
                        m_MaxIndex = m_Writer.m_BlockStream->RangeCount - 1;
                    }

                    if (foreachIndex < m_MinIndex || foreachIndex > m_MaxIndex)
                    {
                        throw new ArgumentException($"Index {foreachIndex} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in NativeEventStream.");
                    }
                }

                if (m_Writer.m_ForeachIndex != int.MinValue)
                {
                    throw new ArgumentException($"BeginForEachIndex must always be balanced by a EndForEachIndex call");
                }

                if (0 != m_Writer.m_BlockStream->Ranges[foreachIndex].ElementCount)
                {
                    throw new ArgumentException($"BeginForEachIndex can only be called once for the same index ({foreachIndex}).");
                }

                Assert.IsTrue(foreachIndex >= 0 && foreachIndex < m_Writer.m_BlockStream->RangeCount);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckEndForEachIndex()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);

                if (m_Writer.m_ForeachIndex == int.MinValue)
                {
                    throw new System.ArgumentException("EndForEachIndex must always be called balanced by a BeginForEachIndex or AppendForEachIndex call");
                }
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckAllocateSize(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);

                if (m_PassByRefCheck != UnsafeUtility.AddressOf(ref this))
                {
                    throw new ArgumentException("NativeEventStream.Writer must be passed by ref once it is in use");
                }

                if (m_Writer.m_ForeachIndex == int.MinValue)
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