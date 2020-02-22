// <copyright file="EventUtility.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using System;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

    /// <summary>
    /// The EventUtility.
    /// </summary>
    public static class EventUtility
    {
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

            if (readers.Count > 0)
            {
                var counter = new NativeArray<int>(readers.Count, Allocator.TempJob);

                for (var i = 0; i < readers.Count; i++)
                {
                    var countHandle = new CountJob
                        {
                            Reader = readers[i].Item1,
                            Count = readers[i].Item2,
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
            public int Count;

            [NativeDisableContainerSafetyRestriction]
            public NativeArray<int> Counter;
            public int Index;

            public void Execute()
            {
                for (var i = 0; i < this.Count; i++)
                {
                    this.Counter[this.Index] += this.Reader.BeginForEachIndex(i);
                }
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