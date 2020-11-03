// <copyright file="UnsafeEventStream.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using System;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    /// <summary>
    /// A thread data stream supporting parallel reading and parallel writing.
    /// Allows you to write different types or arrays into a single stream.
    /// </summary>
    public unsafe partial struct UnsafeEventStream : INativeDisposable, IEquatable<UnsafeEventStream>
    {
        [NativeDisableUnsafePtrRestriction]
        private UnsafeEventStreamBlockData* m_Block;

        private Allocator m_Allocator;

        public UnsafeEventStream(int foreachCount, Allocator allocator)
        {
            AllocateBlock(out this, allocator);
            this.AllocateForEach(foreachCount);
        }

        internal static void AllocateBlock(out UnsafeEventStream stream, Allocator allocator)
        {
            int blockCount = JobsUtility.MaxJobThreadCount;

            int allocationSize = sizeof(UnsafeEventStreamBlockData) + (sizeof(UnsafeEventStreamBlock*) * blockCount);
            byte* buffer = (byte*)UnsafeUtility.Malloc(allocationSize, 16, allocator);
            UnsafeUtility.MemClear(buffer, allocationSize);

            var block = (UnsafeEventStreamBlockData*)buffer;

            stream.m_Block = block;
            stream.m_Allocator = allocator;

            block->Allocator = allocator;
            block->BlockCount = blockCount;
            block->Blocks = (UnsafeEventStreamBlock**)(buffer + sizeof(UnsafeEventStreamBlockData));

            block->Ranges = null;
            block->ThreadRanges = null;
            block->RangeCount = 0;
        }

        internal void AllocateForEach(int forEachCount)
        {
            long allocationSize = sizeof(UnsafeEventStreamRange) * forEachCount;
            long allocationThreadSize = sizeof(UnsafeEventStreamThreadRange) * forEachCount;
            m_Block->Ranges = (UnsafeEventStreamRange*)UnsafeUtility.Malloc(allocationSize, 16, this.m_Allocator);
            m_Block->ThreadRanges = (UnsafeEventStreamThreadRange*)UnsafeUtility.Malloc(allocationThreadSize, 16, this.m_Allocator); // todo conditional
            m_Block->RangeCount = forEachCount;
            UnsafeUtility.MemClear(m_Block->Ranges, allocationSize);
        }

        public bool IsCreated => this.m_Block != null;

        public int ForEachCount => m_Block->RangeCount;

        public bool IsEmpty()
        {
            if (!this.IsCreated)
            {
                return true;
            }

            for (int i = 0; i != m_Block->RangeCount; i++)
            {
                if (m_Block->Ranges[i].ElementCount > 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary> Returns reader instance. </summary>
        /// <returns> Reader instance. </returns>
        public Reader AsReader()
        {
            return new Reader(ref this);
        }

        /// <summary>
        /// Returns writer instance.
        /// </summary>
        /// <returns>Writer instance</returns>
        public IndexWriter AsIndexWriter()
        {
            return new IndexWriter(ref this);
        }

        /// <summary>
        /// Returns writer instance.
        /// </summary>
        /// <returns>Writer instance</returns>
        public ThreadWriter AsThreadWriter()
        {
            return new ThreadWriter(ref this);
        }

        /// <summary>
        /// The current number of items in the container.
        /// </summary>
        /// <returns>The item count.</returns>
        public int Count()
        {
            int itemCount = 0;

            for (int i = 0; i != m_Block->RangeCount; i++)
            {
                itemCount += m_Block->Ranges[i].ElementCount;
            }

            return itemCount;
        }

        /// <summary>
        /// Copies stream data into NativeArray.
        /// </summary>
        /// <typeparam name="T">The type of value.</typeparam>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns>A new NativeArray, allocated with the given strategy and wrapping the stream data.</returns>
        /// <remarks>The array is a copy of stream data.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public NativeArray<T> ToNativeArray<T>(Allocator allocator) where T : struct
        {
            var array = new NativeArray<T>(Count(), allocator, NativeArrayOptions.UninitializedMemory);
            var reader = AsReader();

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

        private void Deallocate()
        {
            if (this.m_Block == null)
            {
                return;
            }

            for (int i = 0; i != m_Block->BlockCount; i++)
            {
                UnsafeEventStreamBlock* block = m_Block->Blocks[i];
                while (block != null)
                {
                    UnsafeEventStreamBlock* next = block->Next;
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
        /// <param name="inputDeps">All jobs spawned will depend on this JobHandle.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that deletes
        /// the container.</returns>
        [BurstCompatible(RequiredUnityDefine = "UNITY_2020_2_OR_NEWER") /* Due to job scheduling on 2020.1 using statics */]
        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = new DisposeJob { Container = this }.Schedule(inputDeps);

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

        public bool Equals(UnsafeEventStream other)
        {
            return this.m_Block == other.m_Block;
        }
    }
}