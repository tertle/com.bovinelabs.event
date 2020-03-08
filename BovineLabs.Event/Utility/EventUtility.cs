// <copyright file="EventUtility.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Utility
{
    using System;
    using BovineLabs.Event.Systems;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

    /// <summary>
    /// The EventUtility.
    /// </summary>
    public static class EventUtility
    {
        /// <summary> Ensure a <see cref="NativeHashMap{TKey,TValue}" /> has the capacity to be filled with all events of a specific type. </summary>
        /// <param name="eventSystem"> The event system for the extension. </param>
        /// <param name="handle"> Input dependencies. </param>
        /// <param name="hashMap"> The <see cref="NativeHashMap{TKey,TValue}"/> to ensure capacity of. </param>
        /// <typeparam name="TE"> The event type. </typeparam>
        /// <typeparam name="TK"> The key type of the <see cref="NativeHashMap{TKey,TValue}"/>. </typeparam>
        /// <typeparam name="TV"> The value type of the <see cref="NativeHashMap{TKey,TValue}"/>. </typeparam>
        /// <returns> The dependency handle. </returns>
        public static JobHandle EnsureHashMapCapacity<TE, TK, TV>(
            this EventSystem eventSystem,
            JobHandle handle,
            NativeHashMap<TK, TV> hashMap)
            where TK : struct, IEquatable<TK>
            where TV : struct
            where TE : struct
        {
            handle = eventSystem.GetEventReaders<TE>(handle, out var readers);

            JobHandle readerHandle = default;

            if (readers.Length > 0)
            {
                var counter = new NativeArray<int>(readers.Length, Allocator.TempJob);

                for (var i = 0; i < readers.Length; i++)
                {
                    var countHandle = new CountJob
                        {
                            Reader = readers[i].Item1,
                            Counter = counter,
                            Index = i,
                        }
                        .Schedule(handle);

                    readerHandle = JobHandle.CombineDependencies(readerHandle, countHandle);
                }

                handle = new EnsureHashMapCapacityJob<TK, TV>
                    {
                        Counter = counter,
                        HashMap = hashMap,
                    }
                    .Schedule(readerHandle);

                handle = counter.Dispose(handle);
            }

            eventSystem.AddJobHandleForConsumer<TE>(handle);

            return handle;
        }

        [BurstCompile]
        private struct CountJob : IJob
        {
            public NativeStream.Reader Reader;

            [NativeDisableContainerSafetyRestriction]
            public NativeArray<int> Counter;
            public int Index;

            public void Execute()
            {
                this.Counter[this.Index] = this.Reader.ComputeItemCount();
            }
        }

        [BurstCompile]
        private struct EnsureHashMapCapacityJob<TK, TV> : IJob
            where TK : struct, IEquatable<TK>
            where TV : struct
        {
            [ReadOnly]
            public NativeArray<int> Counter;

            public NativeHashMap<TK, TV> HashMap;

            public void Execute()
            {
                var count = 0;

                for (var i = 0; i < this.Counter.Length; i++)
                {
                    count += this.Counter[i];
                }

                var requiredSize = this.HashMap.Length + count;

                if (this.HashMap.Capacity < requiredSize)
                {
                    this.HashMap.Capacity = requiredSize;
                }
            }
        }
    }
}