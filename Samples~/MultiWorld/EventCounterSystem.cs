namespace BovineLabs.Samples
{
    using BovineLabs.Common.Containers;
    using BovineLabs.Event;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using UnityEngine;

    /// <summary>
    /// The EventCounterSystem.
    /// </summary>
    public class EventCounterSystem<T> : JobComponentSystem
        where T : EventSystem
    {
        private EventSystem eventSystem;
        private NativeCounter counter;

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            this.eventSystem = this.World.GetOrCreateSystem<T>();
            this.counter = new NativeCounter(Allocator.Persistent);
        }

        /// <inheritdoc/>
        protected override void OnDestroy()
        {
            this.counter.Dispose();
        }

        /// <inheritdoc/>
        protected override JobHandle OnUpdate(JobHandle handle)
        {
            Debug.Log($"{typeof(T)}: Events last frame: {this.counter.Count}");

            this.counter.Count = 0;

            handle = this.eventSystem.GetEventReaders<TestEventEmpty>(handle, out var readers);

            for (var index = 0; index < readers.Count; index++)
            {
                var reader = readers[index];

                handle = new CountJob
                    {
                        Stream = reader.Item1,
                        Counter = this.counter.ToConcurrent(),
                    }
                    .Schedule(reader.Item2, 8, handle);
            }

            this.eventSystem.AddJobHandleForConsumer<TestEventEmpty>(handle);

            return handle;
        }
    }

    // Outside of generic system so it burst compiles.
    [BurstCompile]
    public struct CountJob : IJobParallelFor
    {
        public NativeStream.Reader Stream;

        public NativeCounter.Concurrent Counter;

        public void Execute(int index)
        {
            var count = this.Stream.BeginForEachIndex(index);

            for (var i = 0; i < count; i++)
            {
                this.Counter.Increment();
            }

            this.Stream.EndForEachIndex();
        }
    }
}