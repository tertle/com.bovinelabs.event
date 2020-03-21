// <copyright file="UnsafeThreadStream.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using JetBrains.Annotations;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs.LowLevel.Unsafe;

    [SuppressMessage("ReSharper", "SA1600", Justification = "TODO LATER")]
    public unsafe struct UnsafeThreadStream : IDisposable
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeThreadStreamBlockData* block;

        private Allocator allocator;

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Deallocate();
        }

        public Writer AsWriter()
        {
            return new Writer(ref this);
        }

        internal static void AllocateBlock(out UnsafeThreadStream stream, Allocator allocator)
        {
            int blockCount = JobsUtility.MaxJobThreadCount;

            int allocationSize = sizeof(UnsafeThreadStreamBlockData) + (sizeof(UnsafeThreadStreamBlockData*) * blockCount);
            byte* buffer = (byte*)UnsafeUtility.Malloc(allocationSize, 16, allocator);
            UnsafeUtility.MemClear(buffer, allocationSize);

            var block = (UnsafeThreadStreamBlockData*)buffer;

            stream.block = block;
            stream.allocator = allocator;

            block->Allocator = allocator;
            block->BlockCount = blockCount;
            block->Blocks = (UnsafeThreadStreamBlock**)(buffer + sizeof(UnsafeThreadStreamBlock));

            block->Ranges = null;
            block->RangeCount = 0;
        }

        internal void AllocateForEach(int forEachCount)
        {
            long allocationSize = sizeof(UnsafeThreadStreamRange) * forEachCount;
            this.block->Ranges = (UnsafeThreadStreamRange*)UnsafeUtility.Malloc(allocationSize, 16, this.allocator);
            this.block->RangeCount = forEachCount;
            UnsafeUtility.MemClear(this.block->Ranges, allocationSize);
        }

        private void Deallocate()
        {
            if (this.block == null)
            {
                return;
            }

            for (int i = 0; i != this.block->BlockCount; i++)
            {
                UnsafeThreadStreamBlock* b = this.block->Blocks[i];
                while (b != null)
                {
                    UnsafeThreadStreamBlock* next = b->Next;
                    UnsafeUtility.Free(b, this.allocator);
                    b = next;
                }
            }

            UnsafeUtility.Free(this.block->Ranges, this.allocator);
            UnsafeUtility.Free(this.block, this.allocator);
            this.block = null;
            this.allocator = Allocator.None;
        }

        public struct Writer
        {
            [NativeDisableUnsafePtrRestriction]
            internal readonly UnsafeThreadStreamBlockData* blockStream;

            [NativeDisableUnsafePtrRestriction]
            private UnsafeThreadStreamBlock* currentBlock;

            [NativeDisableUnsafePtrRestriction]
            private byte* currentPtr;

            [NativeDisableUnsafePtrRestriction]
            private byte* currentBlockEnd;

            [NativeDisableUnsafePtrRestriction]
            private UnsafeThreadStreamBlock* firstBlock;

            [NativeSetThreadIndex]
            [UsedImplicitly(ImplicitUseKindFlags.Assign)]
            private int threadIndex;

            internal Writer(ref UnsafeThreadStream stream)
            {
                this.blockStream = stream.block;
                this.currentBlock = null;
                this.currentBlockEnd = null;
                this.currentPtr = null;
                this.firstBlock = null;
                this.threadIndex = 0;

                for (var i = 0; i < JobsUtility.MaxJobThreadCount; i++)
                {
                    this.blockStream->Ranges[i].ElementCount = 0;
                    this.blockStream->Ranges[i].NumberOfBlocks = 0;
                    this.blockStream->Ranges[i].Block = this.currentBlock;
                    this.blockStream->Ranges[i].OffsetInFirstBlock = (int)(this.currentPtr - (byte*)this.currentBlock);
                }
            }

            public void Write<T>(T value)
                where T : struct
            {
                ref var dst = ref this.Allocate<T>();
                dst = value;
            }

            private ref T Allocate<T>()
                where T : struct
            {
                var size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtilityEx.AsRef<T>(this.Allocate(size));
            }

            private byte* Allocate(int size)
            {
                this.firstBlock = this.currentBlock;

                byte* ptr = this.currentPtr;
                this.currentPtr += size;

                if (this.currentPtr > this.currentBlockEnd)
                {
                    UnsafeThreadStreamBlock* oldBlock = this.currentBlock;

                    this.currentBlock = this.blockStream->Allocate(oldBlock, this.threadIndex);
                    this.currentPtr = this.currentBlock->Data;

                    if (this.firstBlock == null)
                    {
                       this.blockStream->Ranges[this.threadIndex].OffsetInFirstBlock = (int)(this.currentPtr - (byte*)this.currentBlock);
                       this.blockStream->Ranges[this.threadIndex].Block = this.currentBlock;
                    }
                    else
                    {
                        this.blockStream->Ranges[this.threadIndex].NumberOfBlocks++;
                    }

                    this.currentBlockEnd = (byte*)this.currentBlock + UnsafeThreadStreamBlockData.AllocationSize;
                    ptr = this.currentPtr;
                    this.currentPtr += size;
                }

                this.blockStream->Ranges[this.threadIndex].ElementCount++;
                this.blockStream->Ranges[this.threadIndex].LastOffset = (int)(this.currentPtr - (byte*)this.currentBlock);

                return ptr;
            }
        }
    }
}