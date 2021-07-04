// <copyright file="UnsafeEventStream.Writer.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;

    public unsafe partial struct UnsafeEventStream
    {
        /// <summary> The writer instance. </summary>
        public struct Writer
        {
            [NativeDisableUnsafePtrRestriction]
            private UnsafeEventStreamBlockData* m_BlockStream;

            [NativeSetThreadIndex]
            private int m_ThreadIndex;

            internal Writer(ref UnsafeEventStream stream)
            {
                this.m_BlockStream = stream.blockData;
                this.m_ThreadIndex = 0; // 0 so main thread works

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

            /// <summary> Write data. </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <param name="value">Value to write.</param>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public void Write<T>(T value)
                where T : struct
            {
                ref var dst = ref this.Allocate<T>();
                dst = value;
            }

            /// <summary> Allocate space for data. </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            /// <returns>Reference to allocated space for data.</returns>
            [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
            public ref T Allocate<T>()
                where T : struct
            {
                var size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtility.AsRef<T>(this.Allocate(size));
            }

            /// <summary> Allocate space for data. </summary>
            /// <param name="size">Size in bytes.</param>
            /// <returns>Pointer to allocated space for data.</returns>
            public byte* Allocate(int size)
            {
                var threadIndex = CollectionHelper.AssumeThreadRange(this.m_ThreadIndex);

                var ptr = this.m_BlockStream->ThreadRanges[threadIndex].CurrentPtr;
                var allocationEnd = ptr + size;
                this.m_BlockStream->ThreadRanges[threadIndex].CurrentPtr = allocationEnd;

                if (allocationEnd > this.m_BlockStream->ThreadRanges[threadIndex].CurrentBlockEnd)
                {
                    var oldBlock = this.m_BlockStream->ThreadRanges[threadIndex].CurrentBlock;
                    var newBlock = this.m_BlockStream->Allocate(oldBlock, threadIndex);

                    this.m_BlockStream->ThreadRanges[threadIndex].CurrentBlock = newBlock;
                    this.m_BlockStream->ThreadRanges[threadIndex].CurrentPtr = newBlock->Data;

                    if (this.m_BlockStream->Ranges[threadIndex].Block == null)
                    {
                        this.m_BlockStream->Ranges[threadIndex].OffsetInFirstBlock = (int)(newBlock->Data - (byte*)newBlock);
                        this.m_BlockStream->Ranges[threadIndex].Block = newBlock;
                    }
                    else
                    {
                        this.m_BlockStream->Ranges[threadIndex].NumberOfBlocks++;
                    }

                    this.m_BlockStream->ThreadRanges[threadIndex].CurrentBlockEnd = (byte*)newBlock + UnsafeEventStreamBlockData.AllocationSize;

                    ptr = newBlock->Data;
                    this.m_BlockStream->ThreadRanges[threadIndex].CurrentPtr = newBlock->Data + size;
                }

                this.m_BlockStream->Ranges[threadIndex].ElementCount++;
                this.m_BlockStream->Ranges[threadIndex].LastOffset = (int)(this.m_BlockStream->ThreadRanges[threadIndex].CurrentPtr -
                                                                           (byte*)this.m_BlockStream->ThreadRanges[threadIndex].CurrentBlock);

                return ptr;
            }
        }
    }
}