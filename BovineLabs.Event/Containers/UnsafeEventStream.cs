namespace BovineLabs.Event.Containers
{
    using System;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using UnityEngine.Assertions;

    internal unsafe struct UnsafeStreamBlock
    {
        internal UnsafeStreamBlock* Next;
        internal fixed byte Data[1];
    }

    internal unsafe struct UnsafeStreamRange
    {
        internal UnsafeStreamBlock* Block;
        internal int OffsetInFirstBlock;
        internal int ElementCount;

        /// One byte past the end of the last byte written
        internal int LastOffset;
        internal int NumberOfBlocks;
    }

    internal unsafe struct UnsafeStreamBlockData
    {
        internal const int AllocationSize = 4 * 1024;
        internal Allocator Allocator;

        internal UnsafeStreamBlock** Blocks;
        internal int BlockCount;

        internal UnsafeStreamRange* Ranges;
        internal int RangeCount;

        internal UnsafeStreamBlock* Allocate(UnsafeStreamBlock* oldBlock, int threadIndex)
        {
            Assert.IsTrue(threadIndex < this.BlockCount && threadIndex >= 0);

            UnsafeStreamBlock* block = (UnsafeStreamBlock*)UnsafeUtility.Malloc(AllocationSize, 16, this.Allocator);
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
                    UnsafeStreamBlock* head = this.Blocks[threadIndex];
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

    /// <summary>
    /// A deterministic data streaming supporting parallel reading and parallel writing.
    /// Allows you to write different types or arrays into a single stream.
    /// </summary>
    public unsafe struct UnsafeEventStream : IDisposable
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeStreamBlockData* m_Block;
        Allocator m_Allocator;

        /// <summary>
        /// Constructs a new UnsafeStream using the specified type of memory allocation.
        /// </summary>
        public UnsafeEventStream(int foreachCount, Allocator allocator)
        {
            AllocateBlock(out this, allocator);
            this.AllocateForEach(foreachCount);
        }

        /// <summary>
        /// Schedule job to construct a new UnsafeStream using the specified type of memory allocation.
        /// </summary>
        /// <param name="dependency">All jobs spawned will depend on this JobHandle.</param>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        public static JobHandle ScheduleConstruct<T>(out UnsafeEventStream stream, NativeList<T> forEachCountFromList, JobHandle dependency, Allocator allocator)
            where T : struct
        {
            AllocateBlock(out stream, allocator);
            var jobData = new ConstructJobList<T> { List = forEachCountFromList, Container = stream };
            return jobData.Schedule(dependency);
        }

        /// <summary>
        /// Schedule job to construct a new UnsafeStream using the specified type of memory allocation.
        /// </summary>
        /// <param name="dependency">All jobs spawned will depend on this JobHandle.</param>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        public static JobHandle ScheduleConstruct(out UnsafeEventStream stream, NativeArray<int> lengthFromIndex0, JobHandle dependency, Allocator allocator)
        {
            AllocateBlock(out stream, allocator);
            var jobData = new ConstructJob { Length = lengthFromIndex0, Container = stream };
            return jobData.Schedule(dependency);
        }

        internal static void AllocateBlock(out UnsafeEventStream stream, Allocator allocator)
        {
            int blockCount = JobsUtility.MaxJobThreadCount;

            int allocationSize = sizeof(UnsafeStreamBlockData) + sizeof(UnsafeStreamBlock*) * blockCount;
            byte* buffer = (byte*)UnsafeUtility.Malloc(allocationSize, 16, allocator);
            UnsafeUtility.MemClear(buffer, allocationSize);

            var block = (UnsafeStreamBlockData*)buffer;

            stream.m_Block = block;
            stream.m_Allocator = allocator;

            block->Allocator = allocator;
            block->BlockCount = blockCount;
            block->Blocks = (UnsafeStreamBlock**)(buffer + sizeof(UnsafeStreamBlockData));

            block->Ranges = null;
            block->RangeCount = 0;
        }

        internal void AllocateForEach(int forEachCount)
        {
            long allocationSize = sizeof(UnsafeStreamRange) * forEachCount;
            this.m_Block->Ranges = (UnsafeStreamRange*)UnsafeUtility.Malloc(allocationSize, 16, this.m_Allocator);
            this.m_Block->RangeCount = forEachCount;
            UnsafeUtility.MemClear(this.m_Block->Ranges, allocationSize);
        }

        /// <summary>
        /// Reports whether memory for the container is allocated.
        /// </summary>
        /// <value>True if this container object's internal storage has been allocated.</value>
        /// <remarks>Note that the container storage is not created if you use the default constructor. You must specify
        /// at least an allocation type to construct a usable container.</remarks>
        public bool IsCreated => this.m_Block != null;

        /// <summary>
        /// </summary>
        public int ForEachCount { get { return this.m_Block->RangeCount; } }

        /// <summary>
        /// Returns reader instance.
        /// </summary>
        public Reader AsReader()
        {
            return new Reader(ref this);
        }

        /// <summary>
        /// Returns writer instance.
        /// </summary>
        public Writer AsWriter()
        {
            return new Writer(ref this);
        }

        /// <summary>
        /// Compute item count.
        /// </summary>
        /// <returns>Item count.</returns>
        public int ComputeItemCount()
        {
            int itemCount = 0;

            for (int i = 0; i != this.m_Block->RangeCount; i++)
            {
                itemCount += this.m_Block->Ranges[i].ElementCount;
            }

            return itemCount;
        }

        /// <summary>
        /// Copies stream data into NativeArray.
        /// </summary>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns>A new NativeArray, allocated with the given strategy and wrapping the stream data.</returns>
        /// <remarks>The array is a copy of stream data.</remarks>
        public NativeArray<T> ToNativeArray<T>(Allocator allocator) where T : struct
        {
            var array = new NativeArray<T>(this.ComputeItemCount(), allocator, NativeArrayOptions.UninitializedMemory);
            var reader = this.AsReader();

            int offset = 0;
            for (int i = 0; i != reader.ForEachCount; i++)
            {
                reader.BeginForEachIndex(i);
                int rangeItemCount = reader.RemainingItemCount;
                for (int j = 0; j < rangeItemCount; ++j)
                {
                    array[offset] = reader.Read<T>();
                    offset++;
                }
                reader.EndForEachIndex();
            }

            return array;
        }

        void Deallocate()
        {
            if (this.m_Block == null)
            {
                return;
            }

            for (int i = 0; i != this.m_Block->BlockCount; i++)
            {
                UnsafeStreamBlock* block = this.m_Block->Blocks[i];
                while (block != null)
                {
                    UnsafeStreamBlock* next = block->Next;
                    UnsafeUtility.Free(block, this.m_Allocator);
                    block = next;
                }
            }

            UnsafeUtility.Free(this.m_Block->Ranges, this.m_Allocator);
            UnsafeUtility.Free(this.m_Block, this.m_Allocator);
            this.m_Block = null;
            this.m_Allocator = Allocator.None;
        }

        /// <summary>
        /// Disposes of this stream and deallocates its memory immediately.
        /// </summary>
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

            this.m_Block = null;

            return jobHandle;
        }

        [BurstCompile]
        struct DisposeJob : IJob
        {
            public UnsafeEventStream Container;

            public void Execute()
            {
                this.Container.Deallocate();
            }
        }

        [BurstCompile]
        struct ConstructJobList<T> : IJob
            where T : struct
        {
            public UnsafeEventStream Container;

            [ReadOnly]
            public NativeList<T> List;

            public void Execute()
            {
                this.Container.AllocateForEach(this.List.Length);
            }
        }

        [BurstCompile]
        struct ConstructJob : IJob
        {
            public UnsafeEventStream Container;

            [ReadOnly]
            public NativeArray<int> Length;

            public void Execute()
            {
                this.Container.AllocateForEach(this.Length[0]);
            }
        }

        /// <summary>
        /// </summary>
        public unsafe struct Writer
        {
            [NativeDisableUnsafePtrRestriction]
            internal UnsafeStreamBlockData* m_BlockStream;

            [NativeDisableUnsafePtrRestriction]
            UnsafeStreamBlock* m_CurrentBlock;

            [NativeDisableUnsafePtrRestriction]
            byte* m_CurrentPtr;

            [NativeDisableUnsafePtrRestriction]
            byte* m_CurrentBlockEnd;

            internal int m_ForeachIndex;
            int m_ElementCount;

            [NativeDisableUnsafePtrRestriction]
            UnsafeStreamBlock* m_FirstBlock;

            int m_FirstOffset;
            int m_NumberOfBlocks;

            [NativeSetThreadIndex]
            int m_ThreadIndex;

            internal Writer(ref UnsafeEventStream stream)
            {
                this.m_BlockStream = stream.m_Block;
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
            public int ForEachCount { get { return this.m_BlockStream->RangeCount; } }

            /// <summary>
            /// Begin reading data at the iteration index.
            /// </summary>
            /// <param name="foreachIndex"></param>
            /// <remarks>BeginForEachIndex must always be called balanced by a EndForEachIndex.</remarks>
            /// <returns>The number of elements at this index.</returns>
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
                this.m_BlockStream->Ranges[this.m_ForeachIndex].ElementCount = this.m_ElementCount;
                this.m_BlockStream->Ranges[this.m_ForeachIndex].OffsetInFirstBlock = this.m_FirstOffset;
                this.m_BlockStream->Ranges[this.m_ForeachIndex].Block = this.m_FirstBlock;

                this.m_BlockStream->Ranges[this.m_ForeachIndex].LastOffset = (int)(this.m_CurrentPtr - (byte*)this.m_CurrentBlock);
                this.m_BlockStream->Ranges[this.m_ForeachIndex].NumberOfBlocks = this.m_NumberOfBlocks;
            }

            /// <summary>
            /// Write data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            public void Write<T>(T value) where T : struct
            {
                ref T dst = ref this.Allocate<T>();
                dst = value;
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            public ref T Allocate<T>() where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtilityEx.AsRef<T>(this.Allocate(size));
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <param name="size">Size in bytes.</param>
            public byte* Allocate(int size)
            {
                byte* ptr = this.m_CurrentPtr;
                this.m_CurrentPtr += size;

                if (this.m_CurrentPtr > this.m_CurrentBlockEnd)
                {
                    UnsafeStreamBlock* oldBlock = this.m_CurrentBlock;

                    this.m_CurrentBlock = this.m_BlockStream->Allocate(oldBlock, this.m_ThreadIndex);
                    this.m_CurrentPtr = this.m_CurrentBlock->Data;

                    if (this.m_FirstBlock == null)
                    {
                        this.m_FirstOffset = (int)(this.m_CurrentPtr - (byte*)this.m_CurrentBlock);
                        this.m_FirstBlock = this.m_CurrentBlock;
                    }
                    else
                    {
                        this.m_NumberOfBlocks++;
                    }

                    this.m_CurrentBlockEnd = (byte*)this.m_CurrentBlock + UnsafeStreamBlockData.AllocationSize;
                    ptr = this.m_CurrentPtr;
                    this.m_CurrentPtr += size;
                }

                this.m_ElementCount++;

                return ptr;
            }
        }

        /// <summary>
        /// </summary>
        public unsafe struct Reader
        {
            [NativeDisableUnsafePtrRestriction]
            internal UnsafeStreamBlockData* m_BlockStream;

            [NativeDisableUnsafePtrRestriction]
            internal UnsafeStreamBlock* m_CurrentBlock;

            [NativeDisableUnsafePtrRestriction]
            internal byte* m_CurrentPtr;

            [NativeDisableUnsafePtrRestriction]
            internal byte* m_CurrentBlockEnd;

            internal int m_RemainingItemCount;
            internal int m_LastBlockSize;

            internal Reader(ref UnsafeEventStream stream)
            {
                this.m_BlockStream = stream.m_Block;
                this.m_CurrentBlock = null;
                this.m_CurrentPtr = null;
                this.m_CurrentBlockEnd = null;
                this.m_RemainingItemCount = 0;
                this.m_LastBlockSize = 0;
            }

            /// <summary>
            /// Begin reading data at the iteration index.
            /// </summary>
            /// <param name="foreachIndex"></param>
            /// <remarks>BeginForEachIndex must always be called balanced by a EndForEachIndex.</remarks>
            /// <returns>The number of elements at this index.</returns>
            public int BeginForEachIndex(int foreachIndex)
            {
                this.m_RemainingItemCount = this.m_BlockStream->Ranges[foreachIndex].ElementCount;
                this.m_LastBlockSize = this.m_BlockStream->Ranges[foreachIndex].LastOffset;

                this.m_CurrentBlock = this.m_BlockStream->Ranges[foreachIndex].Block;
                this.m_CurrentPtr = (byte*)this.m_CurrentBlock + this.m_BlockStream->Ranges[foreachIndex].OffsetInFirstBlock;
                this.m_CurrentBlockEnd = (byte*)this.m_CurrentBlock + UnsafeStreamBlockData.AllocationSize;

                return this.m_RemainingItemCount;
            }

            /// <summary>
            /// Ensures that all data has been read for the active iteration index.
            /// </summary>
            /// <remarks>EndForEachIndex must always be called balanced by a BeginForEachIndex.</remarks>
            public void EndForEachIndex()
            {
            }

            /// <summary>
            /// Returns for each count.
            /// </summary>
            public int ForEachCount { get { return this.m_BlockStream->RangeCount; } }

            /// <summary>
            /// Returns remaining item count.
            /// </summary>
            public int RemainingItemCount { get { return this.m_RemainingItemCount; } }

            /// <summary>
            /// Returns pointer to data.
            /// </summary>
            public byte* ReadUnsafePtr(int size)
            {
                this.m_RemainingItemCount--;

                byte* ptr = this.m_CurrentPtr;
                this.m_CurrentPtr += size;

                if (this.m_CurrentPtr > this.m_CurrentBlockEnd)
                {
                    this.m_CurrentBlock = this.m_CurrentBlock->Next;
                    this.m_CurrentPtr = this.m_CurrentBlock->Data;

                    this.m_CurrentBlockEnd = (byte*)this.m_CurrentBlock + UnsafeStreamBlockData.AllocationSize;

                    ptr = this.m_CurrentPtr;
                    this.m_CurrentPtr += size;
                }

                return ptr;
            }

            /// <summary>
            /// Read data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            public ref T Read<T>() where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtilityEx.AsRef<T>(this.ReadUnsafePtr(size));
            }

            /// <summary>
            /// Peek into data.
            /// </summary>
            /// <typeparam name="T">The type of value.</typeparam>
            public ref T Peek<T>() where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();

                byte* ptr = this.m_CurrentPtr;
                if (ptr + size > this.m_CurrentBlockEnd)
                {
                    ptr = this.m_CurrentBlock->Next->Data;
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
                for (int i = 0; i != this.m_BlockStream->RangeCount; i++)
                {
                    itemCount += this.m_BlockStream->Ranges[i].ElementCount;
                }

                return itemCount;
            }
        }
    }
}
