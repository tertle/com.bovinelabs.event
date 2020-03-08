// <copyright file="EventUtility.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Utility
{
    using System;
    using BovineLabs.Event.Jobs;
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
            var count = eventSystem.GetEventReadersCount<TE>();

            if (count != 0)
            {
                var counter = new NativeArray<int>(count, Allocator.TempJob);

                handle = new CountJob<TE>
                    {
                        Counter = counter,
                    }
                    .Schedule<CountJob<TE>, TE>(eventSystem, handle, true);

                handle = new EnsureHashMapCapacityJob<TK, TV>
                    {
                        Counter = counter,
                        HashMap = hashMap,
                    }
                    .Schedule(handle);

                handle = counter.Dispose(handle);
            }

            return handle;
        }

        [BurstCompile]
        private struct CountJob<T> : IJobEventStream<T>
            where T : struct
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<int> Counter;

            public void Execute(NativeStream.Reader reader, int index)
            {
                this.Counter[index] = reader.ComputeItemCount();
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