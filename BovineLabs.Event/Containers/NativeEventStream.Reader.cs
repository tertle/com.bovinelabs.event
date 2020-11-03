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
        [NativeContainerIsReadOnly]
        public unsafe struct Reader
        {
            UnsafeEventStream.Reader m_Reader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int m_RemainingBlocks;
            internal AtomicSafetyHandle m_Safety;
#endif

            internal Reader(ref NativeEventStream stream)
            {
                m_Reader = stream.stream.AsReader();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_RemainingBlocks = 0;
                m_Safety = stream.m_Safety;
#endif
            }

            /// <summary>
            /// Begin reading data at the iteration index.
            /// </summary>
            /// <param name="foreachIndex"></param>
            /// <remarks>BeginForEachIndex must always be called balanced by a EndForEachIndex.</remarks>
            /// <returns>The number of elements at this index.</returns>
            public int BeginForEachIndex(int foreachIndex)
            {
                CheckBeginForEachIndex(foreachIndex);

                var remainingItemCount = m_Reader.BeginForEachIndex(foreachIndex);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_RemainingBlocks = m_Reader.m_BlockStream->Ranges[foreachIndex].NumberOfBlocks;
                if (m_RemainingBlocks == 0)
                {
                    m_Reader.m_CurrentBlockEnd = (byte*)m_Reader.m_CurrentBlock + m_Reader.m_LastBlockSize;
                }
#endif

                return remainingItemCount;
            }

            /// <summary>
            /// Ensures that all data has been read for the active iteration index.
            /// </summary>
            /// <remarks>EndForEachIndex must always be called balanced by a BeginForEachIndex.</remarks>
            public void EndForEachIndex()
            {
                m_Reader.EndForEachIndex();
                CheckEndForEachIndex();
            }

            /// <summary>
            /// Returns for each count.
            /// </summary>
            public int ForEachCount => this.m_Reader.ForEachCount;

            /// <summary>
            /// Returns remaining item count.
            /// </summary>
            public int RemainingItemCount { get { return m_Reader.RemainingItemCount; } }

            /// <summary>
            /// Returns pointer to data.
            /// </summary>
            /// <param name="size">Size in bytes.</param>
            /// <returns>Pointer to data.</returns>
            public byte* ReadUnsafePtr(int size)
            {
                CheckReadSize(size);

                m_Reader.m_RemainingItemCount--;

                byte* ptr = m_Reader.m_CurrentPtr;
                m_Reader.m_CurrentPtr += size;

                if (m_Reader.m_CurrentPtr > m_Reader.m_CurrentBlockEnd)
                {
                    m_Reader.m_CurrentBlock = m_Reader.m_CurrentBlock->Next;
                    m_Reader.m_CurrentPtr = m_Reader.m_CurrentBlock->Data;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    m_RemainingBlocks--;

                    CheckNotReadingOutOfBounds(size);

                    if (m_RemainingBlocks <= 0)
                    {
                        m_Reader.m_CurrentBlockEnd = (byte*)m_Reader.m_CurrentBlock + m_Reader.m_LastBlockSize;
                    }
                    else
                    {
                        m_Reader.m_CurrentBlockEnd = (byte*)m_Reader.m_CurrentBlock + UnsafeEventStreamBlockData.AllocationSize;
                    }
#else
                    m_Reader.m_CurrentBlockEnd = (byte*)m_Reader.m_CurrentBlock + UnsafeEventStreamBlockData.AllocationSize;
#endif
                    ptr = m_Reader.m_CurrentPtr;
                    m_Reader.m_CurrentPtr += size;
                }

                return ptr;
            }

            /// <summary>
            /// Read data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <returns>Reference to data.</returns>
            public ref T Read<T>() where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtility.AsRef<T>(ReadUnsafePtr(size));
            }

            /// <summary>
            /// Peek into data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <returns>Reference to data.</returns>
            public ref T Peek<T>() where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                CheckReadSize(size);

                return ref m_Reader.Peek<T>();
            }

            /// <summary>
            /// The current number of items in the container.
            /// </summary>
            /// <returns>The item count.</returns>
            public int Count()
            {
                CheckRead();
                return m_Reader.Count();
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckNotReadingOutOfBounds(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (m_RemainingBlocks < 0)
                    throw new System.ArgumentException("Reading out of bounds");

                if (m_RemainingBlocks == 0 && size + sizeof(void*) > m_Reader.m_LastBlockSize)
                    throw new System.ArgumentException("Reading out of bounds");
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckRead()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckReadSize(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);

                Assert.IsTrue(size <= UnsafeEventStreamBlockData.AllocationSize - (sizeof(void*)));
                if (m_Reader.m_RemainingItemCount < 1)
                {
                    throw new ArgumentException("There are no more items left to be read.");
                }
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckBeginForEachIndex(int forEachIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);

                if ((uint)forEachIndex >= (uint)m_Reader.m_BlockStream->RangeCount)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(forEachIndex), $"foreachIndex: {forEachIndex} must be between 0 and ForEachCount: {m_Reader.m_BlockStream->RangeCount}");
                }
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckEndForEachIndex()
            {
                if (m_Reader.m_RemainingItemCount != 0)
                {
                    throw new System.ArgumentException("Not all elements (Count) have been read. If this is intentional, simply skip calling EndForEachIndex();");
                }

                if (m_Reader.m_CurrentBlockEnd != m_Reader.m_CurrentPtr)
                {
                    throw new System.ArgumentException("Not all data (Data Size) has been read. If this is intentional, simply skip calling EndForEachIndex();");
                }
            }
        }
    }
}