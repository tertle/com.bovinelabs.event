// <copyright file="ParallelForProducerSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples
{
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Systems;
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public class ParallelForProducerSystem : SystemBase
    {
        private EventSystem eventSystem;

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            this.eventSystem = this.World.GetOrCreateSystem<EventSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate()
        {
            const int count = 10240;

            var writer = this.eventSystem.CreateEventWriter<TestEventEmpty>();

            this.Dependency = new ProduceJob
                {
                    Events = writer,
                    Random = new Random((uint)UnityEngine.Random.Range(0, int.MaxValue)),
                }
                .Schedule(count, 64, this.Dependency);

            this.eventSystem.AddJobHandleForProducer<TestEventEmpty>(this.Dependency);
        }

        [BurstCompile]
        private struct ProduceJob : IJobParallelFor
        {
            public NativeThreadStream.Writer Events;

            public Random Random;

            public void Execute(int index)
            {
                this.Random.state = (uint)(this.Random.state + index);

                var eventCount = this.Random.NextInt(1, 4096);

                for (var i = 0; i < eventCount; i++)
                {
                    this.Events.Write(default(TestEventEmpty));
                }
            }
        }
    }
}