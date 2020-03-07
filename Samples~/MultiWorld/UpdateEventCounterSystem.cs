// <copyright file="UpdateEventCounterSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples.MultiWorld
{
    using BovineLabs.Event;
    using BovineLabs.Events.Samples.Events;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    /// The FixedEventCounterSystem.
    /// </summary>
    [DisableAutoCreation]
    public class UpdateEventCounterSystem : JobComponentSystem
    {
        private EventSystem eventSystem;

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            this.eventSystem = this.World.GetOrCreateSystem<UpdateEventSystem>();
        }

        /// <inheritdoc/>
        protected override JobHandle OnUpdate(JobHandle handle)
        {
            handle = this.eventSystem.GetEventReaders<TestEventEmpty>(handle, out var readers);

            for (var index = 0; index < readers.Count; index++)
            {
                var reader = readers[index];

                var eventCount = this.eventSystem.CreateEventWriter<UpdateCountEvent>(reader.Item2);

                handle = new CountJob
                    {
                        Stream = reader.Item1,
                        EventCount = eventCount,
                    }
                    .Schedule(reader.Item2, 8, handle);

                this.eventSystem.AddJobHandleForProducer<UpdateCountEvent>(handle);
            }

            this.eventSystem.AddJobHandleForConsumer<TestEventEmpty>(handle);

            return handle;
        }

        // Outside of generic system so it burst compiles.
        [BurstCompile]
        public struct CountJob : IJobParallelFor
        {
            public NativeStream.Writer EventCount;
            public NativeStream.Reader Stream;

            public void Execute(int index)
            {
                var count = this.Stream.BeginForEachIndex(index);

                this.EventCount.BeginForEachIndex(index);
                this.EventCount.Write(new UpdateCountEvent { Value = count });
                this.EventCount.EndForEachIndex();

                // this.Stream.EndForEachIndex();
            }
        }
    }
}