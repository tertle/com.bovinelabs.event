// <copyright file="FixedEventCounterSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples.MultiWorld
{
    using BovineLabs.Event;
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Systems;
    using BovineLabs.Events.Samples.Events;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    /// The FixedEventCounterSystem.
    /// </summary>
    [DisableAutoCreation]
    public class FixedEventCounterSystem : JobComponentSystem
    {
        private EventSystem eventSystem;

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            this.eventSystem = this.World.GetOrCreateSystem<FixedUpdateEventSystem>();
        }

        /// <inheritdoc/>
        protected override JobHandle OnUpdate(JobHandle handle)
        {
            handle = this.eventSystem.GetEventReaders<TestEventEmpty>(handle, out var readers);

            for (var index = 0; index < readers.Count; index++)
            {
                var reader = readers[index];

                var eventCount = this.eventSystem.CreateEventWriter<FixedUpdateCountEvent>();

                handle = new CountJob
                    {
                        Stream = reader,
                        EventCount = eventCount,
                    }
                    .Schedule(reader.ForEachCount, 8, handle);

                this.eventSystem.AddJobHandleForProducer<FixedUpdateCountEvent>(handle);
            }

            this.eventSystem.AddJobHandleForConsumer<TestEventEmpty>(handle);

            return handle;
        }

        // Outside of generic system so it burst compiles.
        [BurstCompile]
        public struct CountJob : IJobParallelFor
        {
            public NativeThreadStream.Writer EventCount;
            public NativeThreadStream.Reader Stream;

            public void Execute(int index)
            {
                var count = this.Stream.BeginForEachIndex(index);
                this.EventCount.Write(count); // will remap to a FixedUpdateCountEvent
            }
        }
    }
}