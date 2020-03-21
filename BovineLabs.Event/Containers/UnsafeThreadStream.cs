// <copyright file="UnsafeThreadStream.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using JetBrains.Annotations;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    [SuppressMessage("ReSharper", "SA1600", Justification = "TODO LATER")]
    [SuppressMessage("ReSharper", "SA1642", Justification = "TODO LATER")]
    [SuppressMessage("ReSharper", "SA1623", Justification = "TODO LATER")]
    [SuppressMessage("ReSharper", "SA1614", Justification = "TODO LATER")]
    [SuppressMessage("ReSharper", "SA1615", Justification = "TODO LATER")]
    [SuppressMessage("ReSharper", "SA1611", Justification = "TODO LATER")]
    public unsafe struct UnsafeThreadStream : IDisposable
    {
        public const int ForEachCount = JobsUtility.MaxJobThreadCount;

        [NativeDisableUnsafePtrRestriction]
        private UnsafeThreadStreamBlockData* block;

        private Allocator allocator;

        /// <summary>
        /// Constructs a new UnsafeStream using the specified type of memory allocation.
        /// </summary>
        public UnsafeThreadStream(Allocator allocator)
        {
            AllocateBlock(out this, allocator);
            this.AllocateForEach();
        }

        /// <summary>
        /// Reports whether memory for the container is allocated.
        /// </summary>
        /// <value>True if this container object's internal storage has been allocated.</value>
        /// <remarks>Note that the container storage is not created if you use the default constructor. You must specify
        /// at least an allocation type to construct a usable container.</remarks>
        public bool IsCreated => this.block != null;

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Deallocate();
        }

        /// <summary>
        /// Safely disposes of this container and deallocates its memory when the jobs that use it have completed.
        /// </summary>
        /// <remarks>You can call this function dispose of the container immediately after scheduling the job. Pass
        /// the [JobHandle](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html) returned by
        /// the [Job.Schedule](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobExtensions.Schedule.html)
        /// method using the `jobHandle` parameter so the job scheduler can dispose the container after all jobs
        /// using it have run.</remarks>
        /// <param name="dependency">All jobs spawned will depend on this JobHandle.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that deletes
        /// the container.</returns>
        public JobHandle Dispose(JobHandle dependency)
        {
            var jobHandle = new DisposeJob { Container = this }.Schedule(dependency);
            this.block = null;
            return jobHandle;
        }

        public Writer AsWriter()
        {
            return new Writer(ref this);
        }

        public Reader AsReader()
        {
            return new Reader(ref this);
        }

        /// <summary>
        /// Compute item count.
        /// </summary>
        /// <returns>Item count.</returns>
        public int ComputeItemCount()
        {
            var itemCount = 0;

            for (var i = 0; i != ForEachCount; i++)
            {
                itemCount += this.block->Ranges[i].ElementCount;
            }

            return itemCount;
        }

        internal static void AllocateBlock(out UnsafeThreadStream stream, Allocator allocator)
        {
            int allocationSize = sizeof(UnsafeThreadStreamBlockData) + (sizeof(UnsafeThreadStreamBlock*) * ForEachCount);
            byte* buffer = (byte*)UnsafeUtility.Malloc(allocationSize, 16, allocator);
            UnsafeUtility.MemClear(buffer, allocationSize);

            var block = (UnsafeThreadStreamBlockData*)buffer;

            stream.block = block;
            stream.allocator = allocator;

            block->Allocator = allocator;
            block->BlockCount = ForEachCount;
            block->Blocks = (UnsafeThreadStreamBlock**)(buffer + sizeof(UnsafeThreadStreamBlockData));

            block->Ranges = null;
        }

        internal void AllocateForEach()
        {
            long allocationSize = sizeof(UnsafeThreadStreamRange) * ForEachCount;
            this.block->Ranges = (UnsafeThreadStreamRange*)UnsafeUtility.Malloc(allocationSize, 16, this.allocator);
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
                var b = this.block->Blocks[i];
                while (b != null)
                {
                    var next = b->Next;
                    UnsafeUtility.Free(b, this.allocator);
                    b = next;
                }
            }

            UnsafeUtility.Free(this.block->Ranges, this.allocator);
            UnsafeUtility.Free(this.block, this.allocator);
            this.block = null;
            this.allocator = Allocator.None;
        }

        [BurstCompile]
        private struct DisposeJob : IJob
        {
            public UnsafeThreadStream Container;

            public void Execute()
            {
                this.Container.Deallocate();
            }
        }

        public struct Writer
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly UnsafeThreadStreamBlockData* blockStream;

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
                this.threadIndex = 0; // 0 so main thread works

                for (var i = 0; i < ForEachCount; i++)
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

            public ref T Allocate<T>()
                where T : struct
            {
                var size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtilityEx.AsRef<T>(this.Allocate(size));
            }

            public byte* Allocate(int size)
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

        public struct Reader
        {
            [NativeDisableUnsafePtrRestriction]
            internal readonly UnsafeThreadStreamBlockData* BlockStream;

            [NativeDisableUnsafePtrRestriction]
            internal UnsafeThreadStreamBlock* CurrentBlock;

            [NativeDisableUnsafePtrRestriction]
            internal byte* CurrentPtr;

            [NativeDisableUnsafePtrRestriction]
            internal byte* CurrentBlockEnd;

            internal int RemainingCount;
            internal int LastBlockSize;

            internal Reader(ref UnsafeThreadStream stream)
            {
                this.BlockStream = stream.block;
                this.CurrentBlock = null;
                this.CurrentPtr = null;
                this.CurrentBlockEnd = null;
                this.RemainingCount = 0;
                this.LastBlockSize = 0;
            }

            /// <summary> Gets the remaining item count. </summary>
            public int RemainingItemCount => this.RemainingCount;

            /// <summary>
            /// Begin reading data at the iteration index.
            /// </summary>
            /// <param name="foreachIndex"></param>
            /// <remarks>BeginForEachIndex must always be called balanced by a EndForEachIndex.</remarks>
            /// <returns>The number of elements at this index.</returns>
            public int BeginForEachIndex(int foreachIndex)
            {
                this.RemainingCount = this.BlockStream->Ranges[foreachIndex].ElementCount;
                this.LastBlockSize = this.BlockStream->Ranges[foreachIndex].LastOffset;

                this.CurrentBlock = this.BlockStream->Ranges[foreachIndex].Block;
                this.CurrentPtr = (byte*)this.CurrentBlock + this.BlockStream->Ranges[foreachIndex].OffsetInFirstBlock;
                this.CurrentBlockEnd = (byte*)this.CurrentBlock + UnsafeThreadStreamBlockData.AllocationSize;

                return this.RemainingCount;
            }

            /// <summary> Returns pointer to data. </summary>
            public byte* ReadUnsafePtr(int size)
            {
                this.RemainingCount--;

                byte* ptr = this.CurrentPtr;
                this.CurrentPtr += size;

                if (this.CurrentPtr > this.CurrentBlockEnd)
                {
                    this.CurrentBlock = this.CurrentBlock->Next;
                    this.CurrentPtr = this.CurrentBlock->Data;

                    this.CurrentBlockEnd = (byte*)this.CurrentBlock + UnsafeThreadStreamBlockData.AllocationSize;

                    ptr = this.CurrentPtr;
                    this.CurrentPtr += size;
                }

                return ptr;
            }

            /// <summary> Read data. </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            public ref T Read<T>()
                where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtilityEx.AsRef<T>(this.ReadUnsafePtr(size));
            }

            /// <summary>
            /// Peek into data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            public ref T Peek<T>()
                where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();

                byte* ptr = this.CurrentPtr;
                if (ptr + size > this.CurrentBlockEnd)
                {
                    ptr = this.CurrentBlock->Next->Data;
                }

                return ref UnsafeUtilityEx.AsRef<T>(ptr);
            }

            /// <summary>
            /// Compute item count.
            /// </summary>
            /// <returns>Item count.</returns>
            public int ComputeItemCount()
            {
                int itemCount = 0;
                for (int i = 0; i != ForEachCount; i++)
                {
                    itemCount += this.BlockStream->Ranges[i].ElementCount;
                }

                return itemCount;
            }
        }
    }
}