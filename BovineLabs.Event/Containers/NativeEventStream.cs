// <copyright file="NativeEventStream.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using JetBrains.Annotations;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using UnityEngine.Assertions;
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// A thread data stream supporting parallel reading and parallel writing.
    /// Allows you to write different types or arrays into a single stream.
    /// </summary>
    [NativeContainer]
    public unsafe struct NativeEventStream : IDisposable, IEquatable<NativeEventStream>
    {
        private UnsafeEventStream stream;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
        private AtomicSafetyHandle m_Safety;

        [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
        [NativeSetClassTypeToNullOnSchedule]
        private DisposeSentinel m_DisposeSentinel;
#endif

        private bool useThreads;

        /// <summary> Initializes a new instance of the <see cref="NativeEventStream"/> struct. </summary>
        /// <param name="allocator"> The specified type of memory allocation. </param>
        public NativeEventStream(Allocator allocator)
        {
            Allocate(out this, allocator, true);
            this.stream.AllocateForEach(JobsUtility.MaxJobThreadCount);
        }

        /// <summary> Initializes a new instance of the <see cref="NativeEventStream"/> struct. </summary>
        /// <param name="foreachCount"> The foreach count. </param>
        /// <param name="allocator"> The specified type of memory allocation. </param>
        public NativeEventStream(int foreachCount, Allocator allocator)
        {
            Allocate(out this, allocator, false);
            this.stream.AllocateForEach(foreachCount);
        }

        /// <summary> Gets a value indicating whether memory for the container is allocated. </summary>
        /// <value> True if this container object's internal storage has been allocated. </value>
        /// <remarks>
        /// <para> Note that the container storage is not created if you use the default constructor.
        /// You must specify at least an allocation type to construct a usable container. </para>
        /// </remarks>
        public bool IsCreated => this.stream.IsCreated;

        /// <summary> Gets the number of streams the container can use. </summary>
        public int ForEachCount => this.stream.ForEachCount;

        /// <summary> Disposes of this stream and deallocates its memory immediately. </summary>
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref this.m_Safety, ref this.m_DisposeSentinel);
#endif
            this.stream.Dispose();
        }

        /// <summary>
        /// Safely disposes of this container and deallocates its memory when the jobs that use it have completed.
        /// </summary>
        /// <remarks> You can call this function dispose of the container immediately after scheduling the job. Pass
        /// the [JobHandle](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html) returned by
        /// the [Job.Schedule](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobExtensions.Schedule.html)
        /// method using the `jobHandle` parameter so the job scheduler can dispose the container after all jobs
        /// using it have run. </remarks>
        /// <param name="dependency"> All jobs spawned will depend on this JobHandle. </param>
        /// <returns> A new job handle containing the prior handles as well as the handle for the job that deletes
        /// the container. </returns>
        public JobHandle Dispose(JobHandle dependency)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // [DeallocateOnJobCompletion] is not supported, but we want the deallocation
            // to happen in a thread. DisposeSentinel needs to be cleared on main thread.
            // AtomicSafetyHandle can be destroyed after the job was scheduled (Job scheduling
            // will check that no jobs are writing to the container).
            DisposeSentinel.Clear(ref this.m_DisposeSentinel);
#endif
            var jobHandle = this.stream.Dispose(dependency);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(this.m_Safety);
#endif
            return jobHandle;
        }

        /// <summary> Returns writer instance. </summary>
        /// <returns> The writer instance. </returns>
        public Writer AsWriter()
        {
            return new Writer(ref this);
        }

        /// <summary> Returns reader instance. </summary>
        /// <returns> The reader instance. </returns>
        public Reader AsReader()
        {
            return new Reader(ref this);
        }

        /// <summary> Compute the item count. </summary>
        /// <returns> Item count. </returns>
        public int ComputeItemCount()
        {
            this.CheckReadAccess();
            return this.stream.ComputeItemCount();
        }

        /// <summary> Copies stream data into NativeArray. </summary>
        /// <param name="allocator"> The specified type of memory allocation. </param>
        /// <typeparam name="T"> The type of the elements in the container. </typeparam>
        /// <returns> A new NativeArray, allocated with the given strategy and wrapping the stream data. </returns>
        /// <remarks> <para> The array is a copy of stream data. </para> </remarks>
        public NativeArray<T> ToNativeArray<T>(Allocator allocator)
            where T : struct
        {
            var array = new NativeArray<T>(this.ComputeItemCount(), allocator, NativeArrayOptions.UninitializedMemory);
            var reader = this.AsReader();

            int offset = 0;
            for (var i = 0; i != this.ForEachCount; i++)
            {
                reader.BeginForEachIndex(i);
                int rangeItemCount = reader.RemainingItemCount;
                for (int j = 0; j < rangeItemCount; ++j)
                {
                    array[offset] = reader.Read<T>();
                    offset++;
                }
            }

            return array;
        }

        /// <inheritdoc/>
        public bool Equals(NativeEventStream other)
        {
            return this.stream.Equals(other.stream);
        }

        /// <inheritdoc/>
        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode", Justification = "Only changes in dispose.")]
        public override int GetHashCode()
        {
            return this.stream.GetHashCode();
        }

        private static void Allocate(out NativeEventStream stream, Allocator allocator, bool useThreads)
        {
            ValidateAllocator(allocator);

            UnsafeEventStream.AllocateBlock(out stream.stream, allocator);
            stream.useThreads = useThreads;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out stream.m_Safety, out stream.m_DisposeSentinel, 0, allocator);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local", Justification = "Point of method")]
        private static void ValidateAllocator(Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator <= Allocator.None)
            {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            }
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
#endif
        }

        /// <summary> The writer instance. </summary>
        [NativeContainer]
        [NativeContainerSupportsMinMaxWriteRestriction]
        [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection and being consistent.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection and being consistent.")]
        public struct Writer
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private readonly bool m_UseThreads;
            [UsedImplicitly(ImplicitUseKindFlags.Assign)]
            private AtomicSafetyHandle m_Safety;
#pragma warning disable 414
            private int m_Length;
#pragma warning restore 414
            private int m_MinIndex;
            private int m_MaxIndex;

            [NativeDisableUnsafePtrRestriction]
            private void* m_PassByRefCheck;
#endif
            private UnsafeEventStream.Writer m_Writer;

            /// <summary> Initializes a new instance of the <see cref="Writer"/> struct. </summary>
            /// <param name="stream"> The stream reference. </param>
            internal Writer(ref NativeEventStream stream)
            {
                this.m_Writer = stream.stream.AsWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.m_Safety = stream.m_Safety;
                this.m_UseThreads = stream.useThreads;

                this.m_Safety = stream.m_Safety;
                this.m_Length = int.MaxValue;
                this.m_MinIndex = int.MinValue;
                this.m_MaxIndex = int.MinValue;
                this.m_PassByRefCheck = null;
#endif
                if (stream.useThreads)
                {
                    this.m_Writer.Index = 0; // for main thread
                }
            }

            /// <summary> Gets the number of streams the container can use. </summary>
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

            /// <summary> Begin reading data at the iteration index. </summary>
            /// <param name="foreachIndex"> The index. </param>
            public void BeginForEachIndex(int foreachIndex)
            {
                this.BeginForEachIndexChecks(foreachIndex);
                this.m_Writer.BeginForEachIndex(foreachIndex);
            }

            /// <summary> Write data. </summary>
            /// <param name="value"> The data to write. </param>
            /// <typeparam name="T"> The type of value. </typeparam>
            public void Write<T>(T value)
                where T : struct
            {
                ref T dst = ref this.Allocate<T>();
                dst = value;
            }

            /// <summary> Allocate space for data. </summary>
            /// <typeparam name="T"> The type of value. </typeparam>
            /// <returns> Reference for the allocated space. </returns>
            public ref T Allocate<T>()
                where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();

#if UNITY_2020_1_OR_NEWER
                return ref UnsafeUtility.AsRef<T>(this.Allocate(size));
#else
                return ref UnsafeUtilityEx.AsRef<T>(this.Allocate(size));
#endif
            }

            /// <summary> Allocate space for data. </summary>
            /// <param name="size"> Size in bytes. </param>
            /// <returns> Pointer for the allocated space. </returns>
            public byte* Allocate(int size)
            {
                this.AllocateChecks(size);
                return this.m_Writer.Allocate(size);
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void BeginForEachIndexChecks(int foreachIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (this.m_UseThreads)
                {
                    throw new InvalidOperationException("Do not call BeginForEachIndex in Thread mode.");
                }

                if (this.m_PassByRefCheck == null)
                {
                    this.m_PassByRefCheck = UnsafeUtility.AddressOf(ref this);
                }

                AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);

                if (foreachIndex < this.m_MinIndex || foreachIndex > this.m_MaxIndex)
                {
                    // When the code is not running through the job system no ParallelForRange patching will occur
                    // We can't grab m_BlockStream->RangeCount on creation of the writer because the RangeCount can be initialized
                    // in a job after creation of the writer
                    if (this.m_MinIndex == int.MinValue && this.m_MaxIndex == int.MinValue)
                    {
                        this.m_MinIndex = 0;
                        this.m_MaxIndex = this.m_Writer.BlockStream->RangeCount - 1;
                    }

                    if (foreachIndex < this.m_MinIndex || foreachIndex > this.m_MaxIndex)
                    {
                        throw new ArgumentException($"Index {foreachIndex} is out of restricted IJobParallelFor range [{this.m_MinIndex}...{this.m_MaxIndex}] in BlockStream.");
                    }
                }

                if (this.m_Writer.BlockStream->Ranges[foreachIndex].ElementCount != 0)
                {
                    throw new ArgumentException($"BeginForEachIndex can only be called once for the same index ({foreachIndex}).");
                }

                Assert.IsTrue(foreachIndex >= 0 && foreachIndex < this.m_Writer.BlockStream->RangeCount);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local", Justification = "Intentional")]
            private void AllocateChecks(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);

                // This is for thread version which doesn't call BeginForEachIndexChecks
                if (this.m_PassByRefCheck == null)
                {
                    this.m_PassByRefCheck = UnsafeUtility.AddressOf(ref this);
                }
                else if (this.m_PassByRefCheck != UnsafeUtility.AddressOf(ref this))
                {
                    throw new ArgumentException("NativeEventStream.Writer must be passed by ref once it is in use");
                }

                if (!this.m_UseThreads && this.m_Writer.Index == int.MinValue)
                {
                    throw new ArgumentException("BeginForEachIndex must be called before Allocate");
                }

                if (size > UnsafeEventStreamBlockData.AllocationSize - sizeof(void*))
                {
                    throw new ArgumentException("Allocation size is too large");
                }
#endif
            }
        }

        /// <summary> The reader instance. </summary>
        [NativeContainer]
        [NativeContainerIsReadOnly]
        public struct Reader
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety system.")]
            [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety system.")]
            [UsedImplicitly(ImplicitUseKindFlags.Assign)]
            private AtomicSafetyHandle m_Safety;
            private int remainingBlocks;
#endif

            private UnsafeEventStream.Reader reader;

            /// <summary> Initializes a new instance of the <see cref="Reader"/> struct. </summary>
            /// <param name="stream"> The stream reference. </param>
            internal Reader(ref NativeEventStream stream)
            {
                this.reader = stream.stream.AsReader();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.remainingBlocks = 0;
                this.m_Safety = stream.m_Safety;
#endif
            }

            /// <summary> Gets the remaining item count. </summary>
            public int RemainingItemCount => this.reader.RemainingItemCount;

            /// <summary> Gets the number of streams the container can use. </summary>
            public int ForEachCount => this.reader.ForEachCount;

            /// <summary> Begin reading data at the iteration index. </summary>
            /// <param name="foreachIndex"> The index to start reading. </param>
            /// <returns> The number of elements at this index. </returns>
            public int BeginForEachIndex(int foreachIndex)
            {
                this.BeginForEachIndexChecks(foreachIndex);

                var remainingItemCount = this.reader.BeginForEachIndex(foreachIndex);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.remainingBlocks = this.reader.BlockStream->Ranges[foreachIndex].NumberOfBlocks;
                if (this.remainingBlocks == 0)
                {
                    this.reader.CurrentBlockEnd = (byte*)this.reader.CurrentBlock + this.reader.LastBlockSize;
                }
#endif

                return remainingItemCount;
            }

            /// <summary> Ensures that all data has been read for the active iteration index. </summary>
            public void EndForEachIndex()
            {
                this.EndForEachIndexChecks();
            }

            /// <summary> Returns pointer to data. </summary>
            /// <param name="size"> The size of data. </param>
            /// <returns> Pointer to data. </returns>
            public byte* ReadUnsafePtr(int size)
            {
                this.ReadChecks(size);

                this.reader.RemainingCount--;

                byte* ptr = this.reader.CurrentPtr;
                this.reader.CurrentPtr += size;

                if (this.reader.CurrentPtr > this.reader.CurrentBlockEnd)
                {
                    this.reader.CurrentBlock = this.reader.CurrentBlock->Next;
                    this.reader.CurrentPtr = this.reader.CurrentBlock->Data;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    this.remainingBlocks--;

                    if (this.remainingBlocks < 0)
                    {
                        Debug.LogError("Reading out of bounds");
                    }

                    if (this.remainingBlocks == 0 && size + sizeof(void*) > this.reader.LastBlockSize)
                    {
                        Debug.LogError("Reading out of bounds");
                    }

                    if (this.remainingBlocks <= 0)
                    {
                        this.reader.CurrentBlockEnd = (byte*)this.reader.CurrentBlock + this.reader.LastBlockSize;
                    }
                    else
                    {
                        this.reader.CurrentBlockEnd = (byte*)this.reader.CurrentBlock + UnsafeEventStreamBlockData.AllocationSize;
                    }
#else
                    this.reader.CurrentBlockEnd = (byte*)this.reader.CurrentBlock + UnsafeEventStreamBlockData.AllocationSize;
#endif
                    ptr = this.reader.CurrentPtr;
                    this.reader.CurrentPtr += size;
                }

                return ptr;
            }

            /// <summary> Read data. </summary>
            /// <typeparam name="T"> The type of value. </typeparam>
            /// <returns> The returned data. </returns>
            public ref T Read<T>()
                where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
#if UNITY_2020_1_OR_NEWER
                return ref UnsafeUtility.AsRef<T>(this.ReadUnsafePtr(size));
#else
                return ref UnsafeUtilityEx.AsRef<T>(this.ReadUnsafePtr(size));
#endif
            }

            /// <summary> Peek into data. </summary>
            /// <typeparam name="T"> The type of value. </typeparam>
            /// <returns> The returned data. </returns>
            public ref T Peek<T>()
                where T : struct
            {
                int size = UnsafeUtility.SizeOf<T>();
                this.ReadChecks(size);

                return ref this.reader.Peek<T>();
            }

            /// <summary> Compute item count. </summary>
            /// <returns> Item count. </returns>
            public int ComputeItemCount()
            {
                this.CheckAccess();
                return this.reader.ComputeItemCount();
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckAccess()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local", Justification = "Intentional")]
            private void ReadChecks(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);

                Assert.IsTrue(size <= UnsafeEventStreamBlockData.AllocationSize - sizeof(void*));
                if (this.reader.RemainingCount < 1)
                {
                    throw new ArgumentException("There are no more items left to be read.");
                }
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void BeginForEachIndexChecks(int forEachIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);

                if (forEachIndex < 0 || forEachIndex >= this.reader.BlockStream->RangeCount)
                {
                    throw new System.ArgumentOutOfRangeException(
                        nameof(forEachIndex),
                        $"foreachIndex: {forEachIndex} must be between 0 and ForEachCount: {this.reader.BlockStream->RangeCount}");
                }
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void EndForEachIndexChecks()
            {
                if (this.reader.RemainingItemCount != 0)
                {
                    throw new System.ArgumentException("Not all elements (Count) have been read. If this is intentional, simply skip calling EndForEachIndex();");
                }

                if (this.reader.CurrentBlockEnd != this.reader.CurrentPtr)
                {
                    throw new System.ArgumentException("Not all data (Data Size) has been read. If this is intentional, simply skip calling EndForEachIndex();");
                }
            }
        }
    }
}