// <copyright file="NativeThreadStreamPerformanceTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.PerformanceTests.Containers
{
    using BovineLabs.Event.Containers;
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Entities.Tests;
    using Unity.PerformanceTesting;

    internal class NativeThreadStreamPerformanceTests : ECSTestsFixture
    {
        private const int EntitiesForEachCount = 1000000;

        [Test]
        [Performance]
        public void WriteEntitiesForEachNativeThreadStream()
        {
            var system = this.World.CreateSystem<EntitiesForEachTest>(EntitiesForEachCount);
            NativeThreadStream<int> stream = default;

            Measure.Method(() => system.NativeThreadStreamTest(stream))
                   .SetUp(() => { stream = new NativeThreadStream<int>(Allocator.TempJob); })
                   .CleanUp(() => stream.Dispose())
                   .Run();
        }

        [Test]
        [Performance]
        public void WriteEntitiesForEachNativeStream()
        {
            var system = this.World.CreateSystem<EntitiesForEachTest>(EntitiesForEachCount);
            NativeStream stream = default;

            Measure.Method(() => system.NativeStreamTest(stream))
                   .SetUp(() => { stream = new NativeStream(EntitiesForEachCount, Allocator.TempJob); })
                   .CleanUp(() => stream.Dispose())
                   .Run();
        }

        [Test]
        [Performance]
        public void WriteEntitiesForEachNativeQueue()
        {
            var system = this.World.CreateSystem<EntitiesForEachTest>(EntitiesForEachCount);
            NativeQueue<int> queue = default;

            Measure.Method(() => system.NativeQueueTest(queue))
                   .SetUp(() => { queue = new NativeQueue<int>(Allocator.TempJob); })
                   .CleanUp(() => queue.Dispose())
                   .Run();
        }

        [DisableAutoCreation]
        private class EntitiesForEachTest : SystemBase
        {
            private const int Archetypes = 16;

            private readonly int count;

            public EntitiesForEachTest(int count)
            {
                this.count = count;
            }

            public void NativeThreadStreamTest(NativeThreadStream<int> stream)
            {
                var writer = stream.AsWriter();

                this.Entities
                    .WithAll<TestComponent>()
                    .ForEach((int entityInQueryIndex) => writer.Write(entityInQueryIndex))
                    .WithBurst(synchronousCompilation: true)
                    .ScheduleParallel();

                this.Dependency.Complete();
            }

            public void NativeStreamTest(NativeStream stream)
            {
                var writer = stream.AsWriter();

                this.Entities
                    .WithAll<TestComponent>()
                    .ForEach((int entityInQueryIndex) =>
                    {
                        writer.BeginForEachIndex(entityInQueryIndex);
                        writer.Write(entityInQueryIndex);
                        writer.EndForEachIndex();
                    })
                    .WithBurst(synchronousCompilation: true)
                    .ScheduleParallel();

                this.Dependency.Complete();
            }

            public void NativeQueueTest(NativeQueue<int> queue)
            {
                var parallel = queue.AsParallelWriter();

                this.Entities
                    .WithAll<TestComponent>()
                    .ForEach((int entityInQueryIndex) => parallel.Enqueue(entityInQueryIndex))
                    .WithBurst(synchronousCompilation: true)
                    .ScheduleParallel();

                this.Dependency.Complete();
            }

            protected override void OnCreate()
            {
                var arch = this.EntityManager.CreateArchetype(typeof(TestComponent));

                using (var entities = new NativeArray<Entity>(this.count, Allocator.TempJob))
                {
                    this.EntityManager.CreateEntity(arch, entities);

                    for (var index = 0; index < entities.Length; index++)
                    {
                        var entity = entities[index];

                        this.EntityManager.SetSharedComponentData(entity, new TestComponent { Chunk = index % Archetypes });
                    }
                }
            }

            protected override void OnUpdate()
            {
            }

            private struct TestComponent : ISharedComponentData
            {
                public int Chunk;
            }
        }
    }
}