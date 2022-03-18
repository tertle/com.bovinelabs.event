// <copyright file="UpdateEventCounterSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples.MultiWorld
{
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Samples.Events;
    using BovineLabs.Event.Systems;
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    /// The FixedEventCounterSystem.
    /// </summary>
    [DisableAutoCreation]
    public partial class UpdateEventCounterSystem : SystemBase
    {
        private EventSystem eventSystem;

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            this.eventSystem = this.World.GetExistingSystem<UpdateEventSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate()
        {
            this.Dependency = this.eventSystem.GetEventReaders<TestEventEmpty>(this.Dependency, out var readers);

            for (var index = 0; index < readers.Count; index++)
            {
                var reader = readers[index];

                var eventCount = this.eventSystem.CreateEventWriter<UpdateCountEvent>();

                this.Dependency = new CountJob
                    {
                        Stream = reader,
                        EventCount = eventCount,
                    }
                    .Schedule(reader.ForEachCount, 8, this.Dependency);

                this.eventSystem.AddJobHandleForProducer<UpdateCountEvent>(this.Dependency);
            }

            this.eventSystem.AddJobHandleForConsumer<TestEventEmpty>(this.Dependency);
        }

        // Outside of generic system so it burst compiles.
        [BurstCompile]
        public struct CountJob : IJobParallelFor
        {
            public NativeEventStream.ThreadWriter EventCount;
            public NativeEventStream.Reader Stream;

            public void Execute(int index)
            {
                var count = this.Stream.BeginForEachIndex(index);
                this.EventCount.Write(new UpdateCountEvent { Value = count });
            }
        }
    }
}