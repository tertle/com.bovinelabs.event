namespace BovineLabs.Event.Containers
{
    using Unity.Assertions;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;

    public unsafe partial struct UnsafeEventStreamNew
    {
        /// <summary> The writer instance. </summary>
        public struct ThreadWriter
        {
            public const int ForEachCount = JobsUtility.MaxJobThreadCount;

            [NativeDisableUnsafePtrRestriction]
            internal UnsafeEventStreamBlockDataNew* m_BlockStream;

            // [NativeDisableUnsafePtrRestriction]
            // private UnsafeEventStreamBlockNew* m_CurrentBlock;
            //
            // [NativeDisableUnsafePtrRestriction]
            // private byte* m_CurrentPtr;
            //
            // [NativeDisableUnsafePtrRestriction]
            // private byte* m_CurrentBlockEnd;
            //
            // internal int m_ForeachIndex;
            // private int m_ElementCount;
            //
            // [NativeDisableUnsafePtrRestriction]
            // private UnsafeEventStreamBlockNew* m_FirstBlock;
            //
            // private int m_FirstOffset;
            // private int m_NumberOfBlocks;

            [NativeSetThreadIndex]
            private int m_ThreadIndex;

            internal ThreadWriter(ref UnsafeEventStreamNew stream)
            {
                this.m_BlockStream = stream.m_Block;
                this.m_ThreadIndex = 0; // 0 so main thread works

                Assert.AreEqual(m_BlockStream->RangeCount, JobsUtility.MaxJobThreadCount);

                for (var i = 0; i < JobsUtility.MaxJobThreadCount; i++)
                {
                    this.m_BlockStream->Ranges[i].ElementCount = 0;
                    this.m_BlockStream->Ranges[i].NumberOfBlocks = 0;
                    this.m_BlockStream->Ranges[i].OffsetInFirstBlock = 0;
                    this.m_BlockStream->Ranges[i].Block = null;
                    this.m_BlockStream->Ranges[i].LastOffset = 0;

                    this.m_BlockStream->ThreadRanges[i].CurrentBlock = null;
                    this.m_BlockStream->ThreadRanges[i].CurrentBlockEnd = null;
                    this.m_BlockStream->ThreadRanges[i].CurrentPtr = null;
                }
            }

            /// <summary>
            /// Write data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <param name="value">Value to write.</param>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public void Write<T>(T value)
                where T : struct
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
                where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtility.AsRef<T>(this.Allocate(size));
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <param name="size">Size in bytes.</param>
            /// <returns>Pointer to allocated space for data.</returns>
            public byte* Allocate(int size)
            {
                byte* ptr = this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentPtr;
                this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentPtr += size;

                if (this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentPtr > this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentBlockEnd)
                {
                    var oldBlock = this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentBlock;

                    this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentBlock = m_BlockStream->Allocate(oldBlock, this.m_ThreadIndex);
                    this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentPtr = this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentBlock->Data;

                    if (this.m_BlockStream->Ranges[this.m_ThreadIndex].Block == null)
                    {
                        this.m_BlockStream->Ranges[this.m_ThreadIndex].OffsetInFirstBlock =
                            (int)(this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentPtr -
                                  (byte*)this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentBlock);

                        this.m_BlockStream->Ranges[this.m_ThreadIndex].Block = this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentBlock;
                    }
                    else
                    {
                        this.m_BlockStream->Ranges[this.m_ThreadIndex].NumberOfBlocks++;
                    }

                    this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentBlockEnd =
                        (byte*)this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentBlock + UnsafeEventStreamBlockDataNew.AllocationSize;

                    ptr = this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentPtr;
                    this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentPtr += size;
                }

                this.m_BlockStream->Ranges[this.m_ThreadIndex].ElementCount++;
                this.m_BlockStream->Ranges[this.m_ThreadIndex].LastOffset =
                    (int)(this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentPtr - (byte*)this.m_BlockStream->ThreadRanges[this.m_ThreadIndex].CurrentBlock);

                return ptr;
            }
        }
    }
}