// <copyright file="UnsafeEventStreamBlockData.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using System.Diagnostics.CodeAnalysis;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine.Assertions;

    [SuppressMessage("ReSharper", "SA1600", Justification = "Private based off UnsafeNativeStreamBlockData.")]
    internal unsafe struct UnsafeEventStreamBlockData
    {
        internal const int AllocationSize = 4 * 1024;
        internal Allocator Allocator;

        internal UnsafeEventStreamBlock** Blocks;
        internal int BlockCount;

        internal UnsafeEventStreamRange* Ranges;
        internal UnsafeEventThreadRange* ThreadRanges;
        internal int RangeCount;

        internal UnsafeEventStreamBlock* Allocate(UnsafeEventStreamBlock* oldBlock, int threadIndex)
        {
            Assert.IsTrue(threadIndex < this.BlockCount && threadIndex >= 0);

            var block = (UnsafeEventStreamBlock*)UnsafeUtility.Malloc(AllocationSize, 16, this.Allocator);
            block->Next = null;

            if (oldBlock == null)
            {
                if (this.Blocks[threadIndex] == null)
                {
                    this.Blocks[threadIndex] = block;
                }
                else
                {
                    // Walk the linked list and append our new block to the end.
                    // Otherwise, we leak memory.
                    UnsafeEventStreamBlock* head = this.Blocks[threadIndex];
                    while (head->Next != null)
                    {
                        head = head->Next;
                    }

                    head->Next = block;
                }
            }
            else
            {
                oldBlock->Next = block;
            }

            return block;
        }
    }

    [SuppressMessage("ReSharper", "SA1600", Justification = "Private based off UnsafeNativeStreamBlock.")]
    internal struct UnsafeEventStreamBlock
    {
        internal unsafe UnsafeEventStreamBlock* Next;
#pragma warning disable 649
        internal unsafe fixed byte Data[1];
#pragma warning restore 649
    }

    [SuppressMessage("ReSharper", "SA1600", Justification = "Private based off UnsafeNativeStreamRange.")]
    internal unsafe struct UnsafeEventStreamRange
    {
        internal UnsafeEventStreamBlock* Block;
        internal int OffsetInFirstBlock;
        internal int ElementCount;

        // One byte past the end of the last byte written
        internal int LastOffset;
        internal int NumberOfBlocks;

        // internal UnsafeEventStreamBlock* CurrentBlock;
        // internal byte* CurrentPtr;
        // internal byte* CurrentBlockEnd;
    }

    [SuppressMessage("ReSharper", "SA1600", Justification = "Private based off UnsafeNativeStreamRange.")]
    internal unsafe struct UnsafeEventThreadRange
    {
        internal UnsafeEventStreamBlock* CurrentBlock;
        internal byte* CurrentPtr;
        internal byte* CurrentBlockEnd;
        internal int ElementCount;
        internal UnsafeEventStreamBlock* FirstBlock;
        internal int FirstOffset;
        internal int NumberOfBlocks;
    }
}