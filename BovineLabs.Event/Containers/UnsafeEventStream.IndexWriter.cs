namespace BovineLabs.Event.Containers
{
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct UnsafeEventStream
    {
        /// <summary> The writer instance. </summary>
        public struct IndexWriter
        {
            [NativeDisableUnsafePtrRestriction]
            internal UnsafeEventStreamBlockData* m_BlockStream;

            [NativeDisableUnsafePtrRestriction]
            private UnsafeEventStreamBlock* m_CurrentBlock;

            [NativeDisableUnsafePtrRestriction]
            private byte* m_CurrentPtr;

            [NativeDisableUnsafePtrRestriction]
            private byte* m_CurrentBlockEnd;

            internal int m_ForeachIndex;
            private int m_ElementCount;

            [NativeDisableUnsafePtrRestriction]
            private UnsafeEventStreamBlock* m_FirstBlock;

            private int m_FirstOffset;
            private int m_NumberOfBlocks;

            [NativeSetThreadIndex]
            private int m_ThreadIndex;

            internal IndexWriter(ref UnsafeEventStream stream)
            {
                this.m_BlockStream = stream.blockData;
                this.m_ForeachIndex = int.MinValue;
                this.m_ElementCount = -1;
                this.m_CurrentBlock = null;
                this.m_CurrentBlockEnd = null;
                this.m_CurrentPtr = null;
                this.m_FirstBlock = null;
                this.m_NumberOfBlocks = 0;
                this.m_FirstOffset = 0;
                this.m_ThreadIndex = 0;
            }

            /// <summary>
            /// </summary>
            public int ForEachCount => m_BlockStream->RangeCount;

            /// <summary>
            /// Begin reading data at the iteration index.
            /// </summary>
            /// <param name="foreachIndex"></param>
            /// <remarks>BeginForEachIndex must always be called balanced by a EndForEachIndex.</remarks>
            public void BeginForEachIndex(int foreachIndex)
            {
                this.m_ForeachIndex = foreachIndex;
                this.m_ElementCount = 0;
                this.m_NumberOfBlocks = 0;
                this.m_FirstBlock = this.m_CurrentBlock;
                this.m_FirstOffset = (int)(this.m_CurrentPtr - (byte*)this.m_CurrentBlock);
            }

            /// <summary>
            /// Ensures that all data has been read for the active iteration index.
            /// </summary>
            /// <remarks>EndForEachIndex must always be called balanced by a BeginForEachIndex.</remarks>
            public void EndForEachIndex()
            {
                m_BlockStream->Ranges[this.m_ForeachIndex].ElementCount = this.m_ElementCount;
                m_BlockStream->Ranges[this.m_ForeachIndex].OffsetInFirstBlock = this.m_FirstOffset;
                m_BlockStream->Ranges[this.m_ForeachIndex].Block = this.m_FirstBlock;

                m_BlockStream->Ranges[this.m_ForeachIndex].LastOffset = (int)(this.m_CurrentPtr - (byte*)this.m_CurrentBlock);
                m_BlockStream->Ranges[this.m_ForeachIndex].NumberOfBlocks = this.m_NumberOfBlocks;
            }

            /// <summary>
            /// Write data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <param name="value">Value to write.</param>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public void Write<T>(T value)
                where T : unmanaged
            {
                ref T dst = ref this.Allocate<T>();
                dst = value;
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <returns>Reference to allocated space for data.</returns>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public ref T Allocate<T>()
                where T : unmanaged
            {
                int size = UnsafeUtility.SizeOf<T>();
#if UNITY_COLLECTIONS_0_14_OR_NEWER
                return ref UnsafeUtility.AsRef<T>(this.Allocate(size));
#else
                return ref UnsafeUtilityEx.AsRef<T>(this.Allocate(size));
#endif
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <param name="size">Size in bytes.</param>
            /// <returns>Pointer to allocated space for data.</returns>
            public byte* Allocate(int size)
            {
                byte* ptr = this.m_CurrentPtr;
                this.m_CurrentPtr += size;

                if (this.m_CurrentPtr > this.m_CurrentBlockEnd)
                {
                    var oldBlock = this.m_CurrentBlock;

                    this.m_CurrentBlock = m_BlockStream->Allocate(oldBlock, this.m_ThreadIndex);
                    this.m_CurrentPtr = m_CurrentBlock->Data;

                    if (this.m_FirstBlock == null)
                    {
                        this.m_FirstOffset = (int)(this.m_CurrentPtr - (byte*)this.m_CurrentBlock);
                        this.m_FirstBlock = this.m_CurrentBlock;
                    }
                    else
                    {
                        this.m_NumberOfBlocks++;
                    }

                    this.m_CurrentBlockEnd = (byte*)this.m_CurrentBlock + UnsafeEventStreamBlockData.AllocationSize;
                    ptr = this.m_CurrentPtr;
                    this.m_CurrentPtr += size;
                }

                this.m_ElementCount++;

                return ptr;
            }
        }
    }
}
