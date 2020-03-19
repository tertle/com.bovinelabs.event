namespace BovineLabs.Event.Containers
{
    using System;
    using System.Diagnostics;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine.Assertions;

    /// <summary>
    /// A deterministic data streaming supporting parallel reading and parallel writing.
    /// Allows you to write different types or arrays into a single stream.
    /// </summary>
    [NativeContainer]
    public unsafe struct NativeStream1 : IDisposable
    {
        UnsafeStream m_Stream;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_Safety;

        [NativeSetClassTypeToNullOnSchedule]
        DisposeSentinel m_DisposeSentinel;
#endif

        /// <summary>
        /// Constructs a new NativeStream using the specified type of memory allocation.
        /// </summary>
        /// <param name="dependency">All jobs spawned will depend on this JobHandle.</param>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        public NativeStream1(int foreachCount, Allocator allocator)
        {
            AllocateBlock(out this, allocator);
            this.m_Stream.AllocateForEach(foreachCount);
        }

        /// <summary>
        /// Schedule job to construct a new NativeStream using the specified type of memory allocation.
        /// </summary>
        /// <param name="dependency">All jobs spawned will depend on this JobHandle.</param>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        public static JobHandle ScheduleConstruct<T>(out NativeStream1 stream, NativeList<T> forEachCountFromList, JobHandle dependency, Allocator allocator)
            where T : struct
        {
            AllocateBlock(out stream, allocator);
            var jobData = new ConstructJobList<T> { List = forEachCountFromList, Container = stream };
            return jobData.Schedule(dependency);
        }

        /// <summary>
        /// Schedule job to construct a new NativeStream using the specified type of memory allocation.
        /// </summary>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        public static JobHandle ScheduleConstruct(out NativeStream1 stream, NativeArray<int> lengthFromIndex0, JobHandle dependency, Allocator allocator)
        {
            AllocateBlock(out stream, allocator);
            var jobData = new ConstructJob { Length = lengthFromIndex0, Container = stream };
            return jobData.Schedule(dependency);
        }

        /// <summary>
        /// Reports whether memory for the container is allocated.
        /// </summary>
        /// <value>True if this container object's internal storage has been allocated.</value>
        /// <remarks>Note that the container storage is not created if you use the default constructor. You must specify
        /// at least an allocation type to construct a usable container.</remarks>
        public bool IsCreated => this.m_Stream.IsCreated;

        /// <summary>
        /// </summary>
        public int ForEachCount
        {
            get
            {
                this.CheckReadAccess();
                return this.m_Stream.ForEachCount;
            }
        }

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
            this.CheckReadAccess();
            return this.m_Stream.ComputeItemCount();
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
            this.CheckReadAccess();
            return this.m_Stream.ToNativeArray<T>(allocator);
        }

        /// <summary>
        /// Disposes of this stream and deallocates its memory immediately.
        /// </summary>
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref this.m_Safety, ref this.m_DisposeSentinel);
#endif
            this.m_Stream.Dispose();
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // [DeallocateOnJobCompletion] is not supported, but we want the deallocation
            // to happen in a thread. DisposeSentinel needs to be cleared on main thread.
            // AtomicSafetyHandle can be destroyed after the job was scheduled (Job scheduling
            // will check that no jobs are writing to the container).
            DisposeSentinel.Clear(ref this.m_DisposeSentinel);
#endif
            var jobHandle = this.m_Stream.Dispose(dependency);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(this.m_Safety);
#endif
            return jobHandle;
        }

        [BurstCompile]
        struct ConstructJobList<T> : IJob
            where T : struct
        {
            public NativeStream1 Container;

            [Unity.Collections.ReadOnly]
            public NativeList<T> List;

            public void Execute()
            {
                this.Container.AllocateForEach(this.List.Length);
            }
        }

        [BurstCompile]
        struct ConstructJob : IJob
        {
            public NativeStream1 Container;

            [Unity.Collections.ReadOnly]
            public NativeArray<int> Length;

            public void Execute()
            {
                this.Container.AllocateForEach(this.Length[0]);
            }
        }

        static void AllocateBlock(out NativeStream1 stream, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator <= Allocator.None)
            {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", "allocator");
            }
#endif
            UnsafeStream.AllocateBlock(out stream.m_Stream, allocator);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out stream.m_Safety, out stream.m_DisposeSentinel, 0, allocator);
#endif
        }

        void AllocateForEach(int forEachCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (forEachCount <= 0)
            {
                throw new ArgumentException("foreachCount must be > 0", "foreachCount");
            }

            Assert.IsTrue(this.m_Stream.m_Block->Ranges == null);
            Assert.AreEqual(0, this.m_Stream.m_Block->RangeCount);
            Assert.AreNotEqual(0, this.m_Stream.m_Block->BlockCount);
#endif

            this.m_Stream.AllocateForEach(forEachCount);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckWriteAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);
#endif
        }

        /// <summary>
        /// </summary>
        [NativeContainer]
        [NativeContainerSupportsMinMaxWriteRestriction]
        public unsafe struct Writer
        {
            UnsafeStream.Writer m_Writer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle m_Safety;
#pragma warning disable CS0414 // warning CS0414: The field 'NativeStream.Writer.m_Length' is assigned but its value is never used
            int m_Length;
#pragma warning restore CS0414
            int m_MinIndex;
            int m_MaxIndex;

            [NativeDisableUnsafePtrRestriction]
            void* m_PassByRefCheck;
#endif

            internal Writer(ref NativeStream1 stream)
            {
                this.m_Writer = stream.m_Stream.AsWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.m_Safety = stream.m_Safety;
                this.m_Length = int.MaxValue;
                this.m_MinIndex = int.MinValue;
                this.m_MaxIndex = int.MinValue;
                this.m_PassByRefCheck = null;
#endif
            }

            /// <summary>
            /// </summary>
            public int ForEachCount
            {
                get
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);
#endif
                    return this.m_Writer.ForEachCount;
                }
            }

            /// <summary>
            /// </summary>
            public void PatchMinMaxRange(int foreEachIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.m_MinIndex = foreEachIndex;
                this.m_MaxIndex = foreEachIndex;
#endif
            }

            /// <summary>
            /// Begin reading data at the iteration index.
            /// </summary>
            /// <param name="foreachIndex"></param>
            /// <remarks>BeginForEachIndex must always be called balanced by a EndForEachIndex.</remarks>
            /// <returns>The number of elements at this index.</returns>
            public void BeginForEachIndex(int foreachIndex)
            {
                //@TODO: Check that no one writes to the same for each index multiple times...
                this.BeginForEachIndexChecks(foreachIndex);
                this.m_Writer.BeginForEachIndex(foreachIndex);
            }

            /// <summary>
            /// Ensures that all data has been read for the active iteration index.
            /// </summary>
            /// <remarks>EndForEachIndex must always be called balanced by a BeginForEachIndex.</remarks>
            public void EndForEachIndex()
            {
                this.EndForEachIndexChecks();
                this.m_Writer.EndForEachIndex();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.m_Writer.m_ForeachIndex = int.MinValue;
#endif
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
                CollectionHelper.CheckIsUnmanaged<T>();
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtilityEx.AsRef<T>(this.Allocate(size));
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <param name="size">Size in bytes.</param>
            public byte* Allocate(int size)
            {
                this.AllocateChecks(size);
                return this.m_Writer.Allocate(size);
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void BeginForEachIndexChecks(int foreachIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);

                if (this.m_PassByRefCheck == null)
                {
                    this.m_PassByRefCheck = UnsafeUtility.AddressOf(ref this);
                }

                if (foreachIndex < this.m_MinIndex || foreachIndex > this.m_MaxIndex)
                {
                    // When the code is not running through the job system no ParallelForRange patching will occur
                    // We can't grab m_BlockStream->RangeCount on creation of the writer because the RangeCount can be initialized
                    // in a job after creation of the writer
                    if (this.m_MinIndex == int.MinValue && this.m_MaxIndex == int.MinValue)
                    {
                        this.m_MinIndex = 0;
                        this.m_MaxIndex = this.m_Writer.m_BlockStream->RangeCount - 1;
                    }

                    if (foreachIndex < this.m_MinIndex || foreachIndex > this.m_MaxIndex)
                    {
                        throw new ArgumentException($"Index {foreachIndex} is out of restricted IJobParallelFor range [{this.m_MinIndex}...{this.m_MaxIndex}] in BlockStream.");
                    }
                }

                if (this.m_Writer.m_ForeachIndex != int.MinValue)
                {
                    throw new ArgumentException($"BeginForEachIndex must always be balanced by a EndForEachIndex call");
                }

                if (0 != this.m_Writer.m_BlockStream->Ranges[foreachIndex].ElementCount)
                {
                    throw new ArgumentException($"BeginForEachIndex can only be called once for the same index ({foreachIndex}).");
                }

                Assert.IsTrue(foreachIndex >= 0 && foreachIndex < this.m_Writer.m_BlockStream->RangeCount);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void EndForEachIndexChecks()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);

                if (this.m_Writer.m_ForeachIndex == int.MinValue)
                {
                    throw new System.ArgumentException("EndForEachIndex must always be called balanced by a BeginForEachIndex or AppendForEachIndex call");
                }
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void AllocateChecks(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);

                if (this.m_PassByRefCheck != UnsafeUtility.AddressOf(ref this))
                {
                    throw new ArgumentException("NativeStream.Writer must be passed by ref once it is in use");
                }

                if (this.m_Writer.m_ForeachIndex == int.MinValue)
                {
                    throw new ArgumentException("Allocate must be called within BeginForEachIndex / EndForEachIndex");
                }

                if (size > UnsafeStreamBlockData.AllocationSize - sizeof(void*))
                {
                    throw new ArgumentException("Allocation size is too large");
                }
#endif
            }
        }

        /// <summary>
        /// </summary>
        [NativeContainer]
        [NativeContainerIsReadOnly]
        public unsafe struct Reader
        {
            UnsafeStream.Reader m_Reader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            int m_RemainingBlocks;
            internal AtomicSafetyHandle m_Safety;
#endif

            internal Reader(ref NativeStream1 stream)
            {
                this.m_Reader = stream.m_Stream.AsReader();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.m_RemainingBlocks = 0;
                this.m_Safety = stream.m_Safety;
#endif
            }

            /// <summary>
            /// Begin reading data at the iteration index.
            /// </summary>
            /// <param name="foreachIndex"></param>
            /// <remarks>BeginForEachIndex must always be called balanced by a EndForEachIndex.</remarks>
            /// <returns>The number of elements at this index.</returns>
            public int BeginForEachIndex(int foreachIndex)
            {
                this.BeginForEachIndexChecks(foreachIndex);

                var remainingItemCount = this.m_Reader.BeginForEachIndex(foreachIndex);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.m_RemainingBlocks = this.m_Reader.m_BlockStream->Ranges[foreachIndex].NumberOfBlocks;
                if (this.m_RemainingBlocks == 0)
                {
                    this.m_Reader.m_CurrentBlockEnd = (byte*)this.m_Reader.m_CurrentBlock + this.m_Reader.m_LastBlockSize;
                }
#endif

                return remainingItemCount;
            }

            /// <summary>
            /// Ensures that all data has been read for the active iteration index.
            /// </summary>
            /// <remarks>EndForEachIndex must always be called balanced by a BeginForEachIndex.</remarks>
            public void EndForEachIndex()
            {
                this.m_Reader.EndForEachIndex();
                this.EndForEachIndexChecks();
            }

            /// <summary>
            /// </summary>
            public int ForEachCount
            {
                get
                {
                    this.CheckAccess();
                    return this.m_Reader.ForEachCount;
                }
            }

            /// <summary>
            /// Returns remaining item count.
            /// </summary>
            public int RemainingItemCount { get { return this.m_Reader.RemainingItemCount; } }

            /// <summary>
            /// Returns pointer to data.
            /// </summary>
            public byte* ReadUnsafePtr(int size)
            {
                this.ReadChecks(size);

                this.m_Reader.m_RemainingItemCount--;

                byte* ptr = this.m_Reader.m_CurrentPtr;
                this.m_Reader.m_CurrentPtr += size;

                if (this.m_Reader.m_CurrentPtr > this.m_Reader.m_CurrentBlockEnd)
                {
                    this.m_Reader.m_CurrentBlock = this.m_Reader.m_CurrentBlock->Next;
                    this.m_Reader.m_CurrentPtr = this.m_Reader.m_CurrentBlock->Data;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    this.m_RemainingBlocks--;

                    if (this.m_RemainingBlocks < 0)
                    {
                        throw new System.ArgumentException("Reading out of bounds");
                    }

                    if (this.m_RemainingBlocks == 0 && size + sizeof(void*) > this.m_Reader.m_LastBlockSize)
                    {
                        throw new System.ArgumentException("Reading out of bounds");
                    }

                    if (this.m_RemainingBlocks <= 0)
                    {
                        this.m_Reader.m_CurrentBlockEnd = (byte*)this.m_Reader.m_CurrentBlock + this.m_Reader.m_LastBlockSize;
                    }
                    else
                    {
                        this.m_Reader.m_CurrentBlockEnd = (byte*)this.m_Reader.m_CurrentBlock + UnsafeStreamBlockData.AllocationSize;
                    }
#else
                    m_Reader.m_CurrentBlockEnd = (byte*)m_Reader.m_CurrentBlock + UnsafeStreamBlockData.AllocationSize;
#endif
                    ptr = this.m_Reader.m_CurrentPtr;
                    this.m_Reader.m_CurrentPtr += size;
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
                this.ReadChecks(size);

                return ref this.m_Reader.Peek<T>();
            }

            /// <summary>
            /// Compute item count.
            /// </summary>
            /// <returns>Item count.</returns>
            public int ComputeItemCount()
            {
                this.CheckAccess();
                return this.m_Reader.ComputeItemCount();
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void CheckAccess()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void ReadChecks(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);

                Assert.IsTrue(size <= UnsafeStreamBlockData.AllocationSize - (sizeof(void*)));
                if (this.m_Reader.m_RemainingItemCount < 1)
                {
                    throw new ArgumentException("There are no more items left to be read.");
                }
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void BeginForEachIndexChecks(int forEachIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);

                if ((uint)forEachIndex >= (uint)this.m_Reader.m_BlockStream->RangeCount)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(forEachIndex), $"foreachIndex: {forEachIndex} must be between 0 and ForEachCount: {this.m_Reader.m_BlockStream->RangeCount}");
                }
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            void EndForEachIndexChecks()
            {
                if (this.m_Reader.m_RemainingItemCount != 0)
                {
                    throw new System.ArgumentException("Not all elements (Count) have been read. If this is intentional, simply skip calling EndForEachIndex();");
                }

                if (this.m_Reader.m_CurrentBlockEnd != this.m_Reader.m_CurrentPtr)
                {
                    throw new System.ArgumentException("Not all data (Data Size) has been read. If this is intentional, simply skip calling EndForEachIndex();");
                }
            }
        }
    }
}
