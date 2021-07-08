// <copyright file="EventConsumer.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using System;
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Jobs;
    using Unity.Assertions;
    using Unity.Collections;
    using Unity.Jobs;

    public unsafe struct EventConsumer<T>
        where T : struct
    {
        internal Consumer* consumer;

        public int ReadersCount => this.consumer->Readers.Length;

        public bool HasReaders => this.consumer->Readers.Length > 0;

        public JobHandle GetReaders(JobHandle jobHandle, out UnsafeListPtr<NativeEventStream.Reader> readers, Allocator allocator = Allocator.Temp)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.consumer->ReadersRequested++;
#endif

            var length = this.consumer->Readers.Length;
            readers = new UnsafeListPtr<NativeEventStream.Reader>(length, allocator); // TODO USE ARRAYPTR
            for (var i = 0; i < length; i++)
            {
                readers.Add(this.consumer->Readers[i].AsReader());
            }

            return JobHandle.CombineDependencies(jobHandle, this.consumer->InputHandle);
        }

        public void AddJobHandle(JobHandle handle)
        {
            this.consumer->JobHandle = handle;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.consumer->HandleSet++;
#endif
        }

        /// <summary>
        /// Ensure a <see cref="NativeHashMap{TKey,TValue}" /> has the capacity to be filled with all events of a specific type.
        /// If the hash map already has elements, it will increase the size so that all events and existing elements can fit.
        /// </summary>
        /// <param name="handle"> Input dependencies. </param>
        /// <param name="hashMap"> The <see cref="NativeHashMap{TKey,TValue}"/> to ensure capacity of. </param>
        /// <typeparam name="TKey"> The key type of the <see cref="NativeHashMap{TKey,TValue}"/> . </typeparam>
        /// <typeparam name="TValue"> The value type of the <see cref="NativeHashMap{TKey,TValue}"/> . </typeparam>
        /// <returns> The dependency handle. </returns>
        public JobHandle EnsureHashMapCapacity<TKey, TValue>(JobHandle handle, NativeHashMap<TKey, TValue> hashMap)
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
            if (this.ReadersCount != 0)
            {
                var counter = new NativeArray<int>(this.ReadersCount, Allocator.TempJob);
                handle = new EventConsumerJobs.CountJob { Counter = counter }.ScheduleParallel(this, handle);
                handle = new EventConsumerJobs.EnsureHashMapCapacityJob<TKey, TValue> { Counter = counter, HashMap = hashMap }.Schedule(handle);
                handle = counter.Dispose(handle);
            }

            return handle;
        }

        /// <summary>
        /// Ensure a <see cref="NativeMultiHashMap{TKey,TValue}" /> has the capacity to be filled with all events of a specific type.
        /// If the hash map already has elements, it will increase the size so that all events and existing elements can fit.
        /// </summary>
        /// <param name="handle"> Input dependencies. </param>
        /// <param name="hashMap"> The <see cref="NativeHashMap{TKey,TValue}"/> to ensure capacity of. </param>
        /// <typeparam name="TKey"> The key type of the <see cref="NativeHashMap{TKey,TValue}"/> . </typeparam>
        /// <typeparam name="TValue"> The value type of the <see cref="NativeHashMap{TKey,TValue}"/> . </typeparam>
        /// <returns> The dependency handle. </returns>
        public JobHandle EnsureHashMapCapacity<TKey, TValue>(JobHandle handle, NativeMultiHashMap<TKey, TValue> hashMap)
            where TKey : struct, IEquatable<TKey>
            where TValue : struct
        {
            if (this.ReadersCount != 0)
            {
                var counter = new NativeArray<int>(this.ReadersCount, Allocator.TempJob);
                handle = new EventConsumerJobs.CountJob { Counter = counter }.ScheduleParallel(this, handle);
                handle = new EventConsumerJobs.EnsureMultiHashMapCapacityJob<TKey, TValue> { Counter = counter, HashMap = hashMap }.Schedule(handle);
                handle = counter.Dispose(handle);
            }

            return handle;
        }

        /// <summary> Get the total number of events of a specific type. </summary>
        /// <param name="handle"> Input dependencies. </param>
        /// <param name="count"> The output array. This must be length of at least 1 and the result will be stored in the index of 0. </param>
        /// <returns> The dependency handle. </returns>
        public JobHandle GetEventCount(JobHandle handle, NativeArray<int> count)
        {
            if (this.ReadersCount != 0)
            {
                var counter = new NativeArray<int>(this.ReadersCount, Allocator.TempJob);
                handle = new EventConsumerJobs.CountJob { Counter = counter }.ScheduleParallel(this, handle);
                handle = new EventConsumerJobs.SumJob { Counter = counter, Count = count }.Schedule(handle);
                counter.Dispose(handle);
            }

            return handle;
        }

        /// <summary> Writes all the events to a new NativeList. </summary>
        /// <param name="handle"> Input dependencies. </param>
        /// <param name="list"> The output list. </param>
        /// <param name="allocator"> The allocator to use on the list. Must be either TempJob or Persistent. </param>
        /// <returns> The dependency handle. </returns>
        public JobHandle ToNativeList(JobHandle handle, out NativeList<T> list, Allocator allocator = Allocator.TempJob)
        {
            Assert.AreNotEqual(Allocator.Temp, allocator, $"Use {Allocator.TempJob} or {Allocator.Persistent}");
            list = new NativeList<T>(128, allocator);
            handle = new EventConsumerJobs.ToNativeListJob<T> { List = list }.Schedule(this, handle);
            return handle;
        }
    }

    internal struct Consumer
    {
        public UnsafeListPtr<NativeEventStream> Readers;
        public JobHandle JobHandle;
        public JobHandle InputHandle;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public int ReadersRequested;
        public int HandleSet;
#endif
    }
}
