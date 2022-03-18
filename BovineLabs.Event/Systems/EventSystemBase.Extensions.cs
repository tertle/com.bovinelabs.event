// <copyright file="EventSystemBase.Extensions.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using System;
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Jobs;
    using Unity.Assertions;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

    /// <content> Extensions methods for the EventSystemBase. </content>
    public partial class EventSystemBase
    {
        /// <summary> The container for common extension methods for events. </summary>
        /// <remarks>
        /// <para> Setup like this rather than as methods to work around having to declare explicit generic arguments.
        /// For example, As{T}().Method(handle, map) instead of .Method{T, TK, TV}(handle, map). </para>
        /// </remarks>
        /// <typeparam name="T"> The event type. </typeparam>
        public readonly struct Extensions<T>
            where T : unmanaged
        {
            private readonly EventSystemBase eventSystem;

            /// <summary> Initializes a new instance of the <see cref="Extensions{T}"/> struct. </summary>
            /// <param name="eventSystem"> Event system parent. </param>
            internal Extensions(EventSystemBase eventSystem)
            {
                this.eventSystem = eventSystem;
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
            public JobHandle EnsureHashMapCapacity<TKey, TValue>(
                JobHandle handle,
                NativeHashMap<TKey, TValue> hashMap)
                where TKey : struct, IEquatable<TKey>
                where TValue : struct
            {
                var readerCount = this.eventSystem.GetEventReadersCount<T>();

                if (readerCount != 0)
                {
                    var counter = new NativeArray<int>(readerCount, Allocator.TempJob);

                    handle = new CountJob { Counter = counter }
                        .ScheduleSimultaneous<CountJob, T>(this.eventSystem, handle);

                    handle = new EnsureHashMapCapacityJob<TKey, TValue>
                        {
                            Counter = counter,
                            HashMap = hashMap,
                        }
                        .Schedule(handle);

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
            public JobHandle EnsureHashMapCapacity<TKey, TValue>(
                JobHandle handle,
                NativeMultiHashMap<TKey, TValue> hashMap)
                where TKey : struct, IEquatable<TKey>
                where TValue : struct
            {
                var readerCount = this.eventSystem.GetEventReadersCount<T>();

                if (readerCount != 0)
                {
                    var counter = new NativeArray<int>(readerCount, Allocator.TempJob);

                    handle = new CountJob { Counter = counter }
                        .ScheduleSimultaneous<CountJob, T>(this.eventSystem, handle);

                    handle = new EnsureMultiHashMapCapacityJob<TKey, TValue>
                        {
                            Counter = counter,
                            HashMap = hashMap,
                        }
                        .Schedule(handle);

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
                var readerCount = this.eventSystem.GetEventReadersCount<T>();

                if (readerCount != 0)
                {
                    var counter = new NativeArray<int>(readerCount, Allocator.TempJob);

                    handle = new CountJob { Counter = counter }
                        .ScheduleSimultaneous<CountJob, T>(this.eventSystem, handle);

                    handle = new SumJob { Counter = counter, Count = count }.Schedule(handle);

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

                handle = new ToNativeListJob { List = list }
                    .Schedule<ToNativeListJob, T>(this.eventSystem, handle);

                return handle;
            }

            [BurstCompile]
            private struct CountJob : IJobEventReader<T>
            {
                [NativeDisableContainerSafetyRestriction]
                public NativeArray<int> Counter;

                public void Execute(NativeEventStream.Reader reader, int readerIndex)
                {
                    this.Counter[readerIndex] = reader.Count();
                }
            }

            [BurstCompile]
            private struct SumJob : IJob
            {
                [ReadOnly]
                public NativeArray<int> Counter;

                [WriteOnly]
                public NativeArray<int> Count;

                public void Execute()
                {
                    var count = 0;

                    for (var i = 0; i < this.Counter.Length; i++)
                    {
                        count += this.Counter[i];
                    }

                    this.Count[0] = count;
                }
            }

            [BurstCompile]
            private struct EnsureHashMapCapacityJob<TKey, TValue> : IJob
                where TKey : struct, IEquatable<TKey>
                where TValue : struct
            {
                [ReadOnly]
                public NativeArray<int> Counter;

                public NativeHashMap<TKey, TValue> HashMap;

                public void Execute()
                {
                    var count = 0;

                    for (var i = 0; i < this.Counter.Length; i++)
                    {
                        count += this.Counter[i];
                    }

                    var requiredSize = this.HashMap.Count() + count;

                    if (this.HashMap.Capacity < requiredSize)
                    {
                        this.HashMap.Capacity = requiredSize;
                    }
                }
            }

            [BurstCompile]
            private struct EnsureMultiHashMapCapacityJob<TKey, TValue> : IJob
                where TKey : struct, IEquatable<TKey>
                where TValue : struct
            {
                [ReadOnly]
                public NativeArray<int> Counter;

                public NativeMultiHashMap<TKey, TValue> HashMap;

                public void Execute()
                {
                    var count = 0;

                    for (var i = 0; i < this.Counter.Length; i++)
                    {
                        count += this.Counter[i];
                    }

                    var requiredSize = this.HashMap.Count() + count;

                    if (this.HashMap.Capacity < requiredSize)
                    {
                        this.HashMap.Capacity = requiredSize;
                    }
                }
            }

            [BurstCompile]
            private struct ToNativeListJob : IJobEvent<T>
            {
                public NativeList<T> List;

                public void Execute(T e)
                {
                    this.List.Add(e);
                }
            }
        }
    }
}
