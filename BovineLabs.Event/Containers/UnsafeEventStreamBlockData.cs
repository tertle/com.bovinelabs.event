namespace BovineLabs.Event.Containers
{
    using System.Diagnostics.CodeAnalysis;
    using Unity.Assertions;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    [BurstCompatible]
    internal unsafe struct UnsafeEventStreamBlock
    {
        internal UnsafeEventStreamBlock* Next;
        internal fixed byte Data[1];
    }

    [BurstCompatible]
    internal unsafe struct UnsafeEventStreamRange
    {
        internal UnsafeEventStreamBlock* Block;
        internal int OffsetInFirstBlock;
        internal int ElementCount;

        // One byte past the end of the last byte written
        internal int LastOffset;
        internal int NumberOfBlocks;
    }

    [BurstCompatible]
    internal unsafe struct UnsafeEventStreamBlockData
    {
        internal const int AllocationSize = 4 * 1024;
        internal Allocator Allocator;

        internal UnsafeEventStreamBlock** Blocks;
        internal int BlockCount;

        internal UnsafeEventStreamRange* Ranges;
        internal UnsafeEventStreamThreadRange* ThreadRanges;
        internal int RangeCount;

        internal UnsafeEventStreamBlock* Allocate(UnsafeEventStreamBlock* oldBlock, int threadIndex)
        {
            Assert.IsTrue(threadIndex < this.BlockCount && threadIndex >= 0);

            var block = (UnsafeEventStreamBlock*)UnsafeUtility.Malloc(AllocationSize, 16, this.Allocator);
            block->Next = null;

            if (oldBlock == null)
            {
                // Append our new block in front of the previous head.
                block->Next = this.Blocks[threadIndex];
                this.Blocks[threadIndex] = block;
            }
            else
            {
                oldBlock->Next = block;
            }

            return block;
        }
    }

    [SuppressMessage("ReSharper", "SA1600", Justification = "Private based off UnsafeNativeStreamRange.")]
    internal unsafe struct UnsafeEventStreamThreadRange
    {
        internal UnsafeEventStreamBlock* CurrentBlock;
        internal byte* CurrentPtr;
        internal byte* CurrentBlockEnd;
    }
}