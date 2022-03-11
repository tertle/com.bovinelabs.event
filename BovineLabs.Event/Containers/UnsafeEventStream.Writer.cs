// <copyright file="UnsafeEventStream.Writer.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using System.Diagnostics.CodeAnalysis;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct UnsafeEventStream
    {
        /// <summary> The writer instance. </summary>
        public struct Writer
        {
            [NativeDisableUnsafePtrRestriction]
            private UnsafeEventStreamBlockData* blockStream;

#pragma warning disable SA1308
            [NativeSetThreadIndex]
            [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by unity scheduler")] // TODO is this true?
            private int m_ThreadIndex;
#pragma warning restore SA1308

            internal Writer(ref UnsafeEventStream stream)
            {
                this.blockStream = stream.blockData;
                this.m_ThreadIndex = 0; // 0 so main thread works
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

                var ptr = this.blockStream->ThreadRanges[threadIndex].CurrentPtr;
                var allocationEnd = ptr + size;
                this.blockStream->ThreadRanges[threadIndex].CurrentPtr = allocationEnd;

                if (allocationEnd > this.blockStream->ThreadRanges[threadIndex].CurrentBlockEnd)
                {
                    var oldBlock = this.blockStream->ThreadRanges[threadIndex].CurrentBlock;
                    var newBlock = this.blockStream->Allocate(oldBlock, threadIndex);

                    this.blockStream->ThreadRanges[threadIndex].CurrentBlock = newBlock;
                    this.blockStream->ThreadRanges[threadIndex].CurrentPtr = newBlock->Data;

                    if (this.blockStream->Ranges[threadIndex].Block == null)
                    {
                        this.blockStream->Ranges[threadIndex].OffsetInFirstBlock = (int)(newBlock->Data - (byte*)newBlock);
                        this.blockStream->Ranges[threadIndex].Block = newBlock;
                    }
                    else
                    {
                        this.blockStream->Ranges[threadIndex].NumberOfBlocks++;
                    }

                    this.blockStream->ThreadRanges[threadIndex].CurrentBlockEnd = (byte*)newBlock + UnsafeEventStreamBlockData.AllocationSize;

                    ptr = newBlock->Data;
                    this.blockStream->ThreadRanges[threadIndex].CurrentPtr = newBlock->Data + size;
                }

                this.blockStream->Ranges[threadIndex].ElementCount++;
                this.blockStream->Ranges[threadIndex].LastOffset = (int)(this.blockStream->ThreadRanges[threadIndex].CurrentPtr -
                                                                           (byte*)this.blockStream->ThreadRanges[threadIndex].CurrentBlock);

                return ptr;
            }
        }
    }
}