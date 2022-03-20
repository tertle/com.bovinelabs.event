// <copyright file="UnsafeListPtr.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using System;
    using Unity.Burst;
    using Unity.Burst.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

    public unsafe struct UnsafeListPtr<T> : INativeDisposable
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private UnsafeList<T>* listData;

        /// <summary>
        /// Constructs a new list using the specified type of memory allocation.
        /// </summary>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <remarks>The list initially has a capacity of one. To avoid reallocating memory for the list, specify
        /// sufficient capacity up front.</remarks>
        public UnsafeListPtr(Allocator allocator)
            : this(1, allocator)
        {
        }

        /// <summary> Constructs a new list with the specified initial capacity and type of memory allocation. </summary>
        /// <param name="initialCapacity">The initial capacity of the list. If the list grows larger than its capacity,
        /// the internal array is copied to a new, larger array.</param>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        public UnsafeListPtr(int initialCapacity, Allocator allocator)
        {
            this.listData = UnsafeList<T>.Create(initialCapacity, allocator);
        }

        /// <summary>
        /// Retrieve a member of the contaner by index.
        /// </summary>
        /// <param name="index">The zero-based index into the list.</param>
        /// <value>The list item at the specified index.</value>
        /// <exception cref="IndexOutOfRangeException">Thrown if index is negative or >= to <see cref="Length"/>.</exception>
        public T this[int index]
        {
            get => UnsafeUtility.ReadArrayElement<T>(this.listData->Ptr, AssumePositive(index));
            set => UnsafeUtility.WriteArrayElement(this.listData->Ptr, AssumePositive(index), value);
        }

        /// <summary>
        /// The current number of items in the list.
        /// </summary>
        /// <value>The item count.</value>
        public int Length
        {
            get => AssumePositive(this.listData->Length);
            set => this.listData->Resize(value, NativeArrayOptions.ClearMemory);
        }

        /// <summary>
        /// The number of items that can fit in the list.
        /// </summary>
        /// <value>The number of items that the list can hold before it resizes its internal storage.</value>
        /// <remarks>Capacity specifies the number of items the list can currently hold. You can change Capacity
        /// to fit more or fewer items. Changing Capacity creates a new array of the specified size, copies the
        /// old array to the new one, and then deallocates the original array memory. You cannot change the Capacity
        /// to a size smaller than <see cref="Length"/> (remove unwanted elements from the list first).</remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if Capacity is set smaller than Length.</exception>
        public int Capacity
        {
            get => AssumePositive(this.listData->Capacity);
            set => this.listData->SetCapacity(value);
        }

        /// <summary>
        /// Adds an element to the list.
        /// </summary>
        /// <param name="value">The struct to be added at the end of the list.</param>
        /// <remarks>If the list has reached its current capacity, it copies the original, internal array to
        /// a new, larger array, and then deallocates the original.
        /// </remarks>
        public void Add(in T value)
        {
            this.listData->Add(value);
        }

        /// <summary>
        /// Truncates the list by replacing the item at the specified index with the last item in the list. The list
        /// is shortened by one.
        /// </summary>
        /// <param name="index">The index of the item to delete.</param>
        /// <exception cref="ArgumentOutOfRangeException">If index is negative or >= <see cref="Length"/>.</exception>
        public void RemoveAtSwapBack(int index)
        {
            this.listData->RemoveAtSwapBack(AssumePositive(index));
        }

        /// <summary>
        /// Reports whether memory for the container is allocated.
        /// </summary>
        /// <value>True if this container object's internal storage has been allocated.</value>
        /// <remarks>
        /// Note that the container storage is not created if you use the default constructor. You must specify
        /// at least an allocation type to construct a usable container.
        ///
        /// *Warning:* the `IsCreated` property can't be used to determine whether a copy of a container is still valid.
        /// If you dispose any copy of the container, the container storage is deallocated. However, the properties of
        /// the other copies of the container (including the original) are not updated. As a result the `IsCreated` property
        /// of the copies still return `true` even though the container storage has been deallocated.
        /// Accessing the data of a native container that has been disposed throws a <see cref='InvalidOperationException'/> exception.
        /// </remarks>
        public bool IsCreated => this.listData != null;

        /// <summary>
        /// Disposes of this container and deallocates its memory immediately.
        /// </summary>
        public void Dispose()
        {
            UnsafeList<T>.Destroy(this.listData);
            this.listData = null;
        }

        /// <summary>
        /// Safely disposes of this container and deallocates its memory when the jobs that use it have completed.
        /// </summary>
        /// <remarks>You can call this function dispose of the container immediately after scheduling the job. Pass
        /// the [JobHandle](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html) returned by
        /// the [Job.Schedule](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobExtensions.Schedule.html)
        /// method using the `jobHandle` parameter so the job scheduler can dispose the container after all jobs
        /// using it have run.</remarks>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that deletes
        /// the container.</returns>
        [BurstCompatible(RequiredUnityDefine = "UNITY_2020_2_OR_NEWER") /* Due to job scheduling on 2020.1 using statics */]
        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = new NativeListDisposeJob { Data = new NativeListDispose { m_ListData = this.listData } }.Schedule(inputDeps);
            this.listData = null;

            return jobHandle;
        }

        /// <summary>
        /// Clears the list.
        /// </summary>
        /// <remarks>List <see cref="Capacity"/> remains unchanged.</remarks>
        public void Clear()
        {
            this.listData->Clear();
        }

        [BurstCompatible]
        internal struct NativeListDispose
        {
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<T>* m_ListData;

            public void Dispose()
            {
                UnsafeList<T>.Destroy(this.m_ListData);
            }
        }

        [BurstCompile]
        [BurstCompatible]
        internal struct NativeListDisposeJob : IJob
        {
            internal NativeListDispose Data;

            public void Execute()
            {
                this.Data.Dispose();
            }
        }

        [return: AssumeRange(0, int.MaxValue)]
        internal static int AssumePositive(int value)
        {
            return value;
        }
    }
}