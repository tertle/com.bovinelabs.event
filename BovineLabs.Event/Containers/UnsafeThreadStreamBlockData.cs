﻿// <copyright file="UnsafeThreadStreamBlockData.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using System.Diagnostics.CodeAnalysis;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine.Assertions;

    [SuppressMessage("ReSharper", "SA1600", Justification = "Private based off UnsafeNativeStreamBlockData.")]
    internal unsafe struct UnsafeThreadStreamBlockData
    {
        internal const int AllocationSize = 4 * 1024;
        internal Allocator Allocator;

        internal UnsafeThreadStreamBlock** Blocks;
        internal int BlockCount;

        internal UnsafeThreadStreamRange* Ranges;

        internal UnsafeThreadStreamBlock* Allocate(UnsafeThreadStreamBlock* oldBlock, int threadIndex)
        {
            Assert.IsTrue(threadIndex < this.BlockCount && threadIndex >= 0);

            var block = (UnsafeThreadStreamBlock*)UnsafeUtility.Malloc(AllocationSize, 16, this.Allocator);
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
                    UnsafeThreadStreamBlock* head = this.Blocks[threadIndex];
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
    internal struct UnsafeThreadStreamBlock
    {
        internal unsafe UnsafeThreadStreamBlock* Next;
#pragma warning disable 649
        internal unsafe fixed byte Data[1];
#pragma warning restore 649
    }

    [SuppressMessage("ReSharper", "SA1600", Justification = "Private based off UnsafeNativeStreamRange.")]
    internal unsafe struct UnsafeThreadStreamRange
    {
        internal UnsafeThreadStreamBlock* Block;
        internal int OffsetInFirstBlock;
        internal int ElementCount;

        // One byte past the end of the last byte written
        internal int LastOffset;
        internal int NumberOfBlocks;

        internal UnsafeThreadStreamBlock* CurrentBlock;
        internal byte* CurrentPtr;
        internal byte* CurrentBlockEnd;
    }
}