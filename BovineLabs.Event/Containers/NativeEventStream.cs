// <copyright file="NativeEventStream.cs" company="BovineLabs">
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
    using Unity.Jobs.LowLevel.Unsafe;
    using Assert = Unity.Assertions.Assert;

    /// <summary>
    /// A thread data stream supporting parallel reading and parallel writing.
    /// Allows you to write different types or arrays into a single stream.
    /// </summary>
    [NativeContainer]
    public unsafe partial struct NativeEventStream : IDisposable, IEquatable<NativeEventStream>
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private bool useThreads;

        [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
        private AtomicSafetyHandle m_Safety;

        [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
        [NativeSetClassTypeToNullOnSchedule]
        private DisposeSentinel m_DisposeSentinel;
#endif

        private UnsafeEventStream stream;

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

        public bool IsEmpty()
        {
            return this.stream.IsEmpty();
        }

        /// <summary> Returns reader instance. </summary>
        /// <returns> The reader instance. </returns>
        public Reader AsReader()
        {
            return new Reader(ref this);
        }

        /// <summary> Returns writer instance. </summary>
        /// <returns> The writer instance. </returns>
        public IndexWriter AsIndexWriter()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsFalse(this.useThreads, "Not in index mode.");
#endif
            return new IndexWriter(ref this);
        }

        /// <summary> Returns writer instance. </summary>
        /// <returns> The writer instance. </returns>
        public ThreadWriter AsThreadWriter()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsTrue(this.useThreads, "Not in thread mode.");
#endif
            return new ThreadWriter(ref this);
        }

        /// <summary>
        /// The current number of items in the container.
        /// </summary>
        /// <returns>The item count.</returns>
        public int Count()
        {
            this.CheckReadAccess();
            return this.stream.Count();
        }

        /// <summary>
        /// Copies stream data into NativeArray.
        /// </summary>
        /// <typeparam name="T">The type of value.</typeparam>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns>A new NativeArray, allocated with the given strategy and wrapping the stream data.</returns>
        /// <remarks>The array is a copy of stream data.</remarks>
        /// <returns></returns>
        public NativeArray<T> ToNativeArray<T>(Allocator allocator)
            where T : unmanaged
        {
            this.CheckReadAccess();
            return this.stream.ToNativeArray<T>(allocator);
        }

        /// <summary>
        /// Disposes of this stream and deallocates its memory immediately.
        /// </summary>
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
            var jobHandle = stream.Dispose(dependency);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(this.m_Safety);
#endif
            return jobHandle;
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

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            stream.useThreads = useThreads;
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
    }
}
