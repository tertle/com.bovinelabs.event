// <copyright file="EventConsumerJobs.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using System;
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Jobs;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

    /// <summary> Jobs that EventConsumer uses. </summary>
    internal static class EventConsumerJobs
    {
        [BurstCompile]
        public struct CountJob : IJobEventReader
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<int> Counter;

            public void Execute(NativeEventStream.Reader reader, int readerIndex)
            {
                this.Counter[readerIndex] = reader.Count();
            }
        }

        [BurstCompile]
        public struct SumJob : IJob
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
        public struct EnsureHashMapCapacityJob<TKey, TValue> : IJob
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            [ReadOnly]
            public NativeArray<int> Counter;

            public NativeParallelHashMap<TKey, TValue> HashMap;

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
        public struct EnsureMultiHashMapCapacityJob<TKey, TValue> : IJob
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
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
        public struct ToNativeListJob<T> : IJobEvent<T>
            where T : unmanaged
        {
            public NativeList<T> List;

            public void Execute(T e)
            {
                this.List.Add(e);
            }
        }
    }
}
