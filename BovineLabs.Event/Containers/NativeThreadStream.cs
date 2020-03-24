// <copyright file="NativeThreadStream.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine.Assertions;

    /// <summary>
    /// A thread data stream supporting parallel reading and parallel writing.
    /// Allows you to write different types or arrays into a single stream.
    /// </summary>
    [NativeContainer]
    public unsafe struct NativeThreadStream : IDisposable, IEquatable<NativeThreadStream>
    {
        private UnsafeThreadStream stream;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
        private AtomicSafetyHandle m_Safety;

        [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
        [NativeSetClassTypeToNullOnSchedule]
        private DisposeSentinel m_DisposeSentinel;
#endif

        /// <summary> Initializes a new instance of the <see cref="NativeThreadStream"/> struct. </summary>
        /// <param name="allocator"> The specified type of memory allocation. </param>
        public NativeThreadStream(Allocator allocator)
        {
            Allocate(out this, allocator);
            this.stream.AllocateForEach();
        }

        /// <summary> Gets a value indicating whether memory for the container is allocated. </summary>
        /// <value> True if this container object's internal storage has been allocated. </value>
        /// <remarks>
        /// <para> Note that the container storage is not created if you use the default constructor.
        /// You must specify at least an allocation type to construct a usable container. </para>
        /// </remarks>
        public bool IsCreated => this.stream.IsCreated;

        /// <summary> Gets the number of streams the container can use. </summary>
        public int ForEachCount => UnsafeThreadStream.ForEachCount;

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
        public bool Equals(NativeThreadStream other)
        {
            return this.stream.Equals(other.stream);
        }

        /// <inheritdoc/>
        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode", Justification = "Only changes in dispose.")]
        public override int GetHashCode()
        {
            return this.stream.GetHashCode();
        }

        private static void Allocate(out NativeThreadStream stream, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator <= Allocator.None)
            {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", "allocator");
            }
#endif
            UnsafeThreadStream.AllocateBlock(out stream.stream, allocator);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out stream.m_Safety, out stream.m_DisposeSentinel, 0, allocator);
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
        [NativeContainerIsAtomicWriteOnly]
        public struct Writer
        {
            private UnsafeThreadStream.Writer writer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
            [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
            private AtomicSafetyHandle m_Safety;
#endif

            /// <summary> Initializes a new instance of the <see cref="Writer"/> struct. </summary>
            /// <param name="stream"> The stream reference. </param>
            internal Writer(ref NativeThreadStream stream)
            {
                this.writer = stream.stream.AsWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.m_Safety = stream.m_Safety;
#endif
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
                CollectionHelper.CheckIsUnmanaged<T>();
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtilityEx.AsRef<T>(this.Allocate(size));
            }

            /// <summary> Allocate space for data. </summary>
            /// <param name="size"> Size in bytes. </param>
            /// <returns> Pointer for the allocated space. </returns>
            public byte* Allocate(int size)
            {
                this.AllocateChecks(size);
                return this.writer.Allocate(size);
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local", Justification = "Intentional")]
            private void AllocateChecks(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);

                if (size > UnsafeThreadStreamBlockData.AllocationSize - sizeof(void*))
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
            private UnsafeThreadStream.Reader reader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private int remainingBlocks;
            [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
            [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
            private AtomicSafetyHandle m_Safety;
#endif

            /// <summary> Initializes a new instance of the <see cref="Reader"/> struct. </summary>
            /// <param name="stream"> The stream reference. </param>
            internal Reader(ref NativeThreadStream stream)
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
            public int ForEachCount => UnsafeThreadStream.ForEachCount;

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
                        throw new System.ArgumentException("Reading out of bounds");
                    }

                    if (this.remainingBlocks == 0 && size + sizeof(void*) > this.reader.LastBlockSize)
                    {
                        throw new System.ArgumentException("Reading out of bounds");
                    }

                    if (this.remainingBlocks <= 0)
                    {
                        this.reader.CurrentBlockEnd = (byte*)this.reader.CurrentBlock + this.reader.LastBlockSize;
                    }
                    else
                    {
                        this.reader.CurrentBlockEnd = (byte*)this.reader.CurrentBlock + UnsafeThreadStreamBlockData.AllocationSize;
                    }
#else
                    this.reader.CurrentBlockEnd = (byte*)this.reader.CurrentBlock + UnsafeThreadStreamBlockData.AllocationSize;
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
                return ref UnsafeUtilityEx.AsRef<T>(this.ReadUnsafePtr(size));
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

                Assert.IsTrue(size <= UnsafeThreadStreamBlockData.AllocationSize - sizeof(void*));
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

                if (forEachIndex < 0 || forEachIndex >= UnsafeThreadStream.ForEachCount)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(forEachIndex), $"foreachIndex: {forEachIndex} must be between 0 and ForEachCount: {UnsafeThreadStream.ForEachCount}");
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