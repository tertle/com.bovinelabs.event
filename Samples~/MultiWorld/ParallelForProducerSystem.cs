// <copyright file="ParallelForProducerSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples
{
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Jobs;
    using BovineLabs.Event.Samples.Events;
    using BovineLabs.Event.Systems;
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Jobs;

    [DisableAutoCreation]
    public class ParallelForProducerSystem : SystemBase
    {
        private EventSystem eventSystem;

        public static int Threads { get; set; } = 128;
        public static int Writers { get; set; } = 1;
        public static int EventsPerThread { get; set; } = 100000;

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            this.eventSystem = this.World.GetOrCreateSystem<EventSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate()
        {
            var outputDependency = this.Dependency;

            for (var i = 0; i < Writers; i++)
            {
                var writer = this.eventSystem.CreateEventWriter<TestEventEmpty>();

                var dependency = new ProduceJob
                    {
                        Events = writer,
                        EventCount = EventsPerThread,
                    }
                    .Schedule(Threads, 1, this.Dependency);

                this.eventSystem.AddJobHandleForProducer<TestEventEmpty>(dependency);

                outputDependency = JobHandle.CombineDependencies(outputDependency, dependency);
            }

            this.Dependency = outputDependency;
        }

        [BurstCompile]
        private struct ProduceJob : IJobParallelFor
        {
            public NativeEventStream.Writer Events;

            public int EventCount;

            public void Execute(int index)
            {
                for (var i = 0; i < this.EventCount; i++)
                {
                    this.Events.Write(default(TestEventEmpty));
                }
            }
        }
    }
}