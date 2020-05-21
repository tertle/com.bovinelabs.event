// <copyright file="NativeThreadStreamPerformanceTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>
#if PERFORMANCE_TESTING
namespace BovineLabs.Event.PerformanceTests.Containers
{
    using BovineLabs.Event.Containers;
    using JetBrains.Annotations;
    using NUnit.Framework;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Entities.Tests;
    using Unity.Jobs;
    using Unity.PerformanceTesting;

    /// <summary> Performance tests for <see cref="NativeThreadStream"/>. </summary>
    /// <remarks><para> Includes comparison tests for <see cref="NativeQueue{T}"/> and <see cref="NativeStream"/>. </para></remarks>
    internal class NativeThreadStreamPerformanceTests : ECSTestsFixture
    {
        /// <summary> Tests performance of writing entities in a <see cref="NativeThreadStream"/> in an Entities.ForEach. </summary>
        /// <param name="entities"> Number of entities to write. </param>
        /// <param name="archetypes"> Number of archetypes to use. </param>
        [Test]
        [Performance]
        public void WriteEntitiesForEachNativeThreadStream(
            [Values(10000, 1000000)] int entities,
            [Values(8, 256)] int archetypes)
        {
            var system = this.World.AddSystem(new EntitiesForEachTest(entities, archetypes));
            NativeThreadStream<int> stream = default;

            Measure.Method(() => system.NativeThreadStreamTest(stream))
                .SetUp(() => { stream = new NativeThreadStream<int>(Allocator.TempJob); })
                .CleanUp(() => stream.Dispose())
                .Run();
        }

        /// <summary> Tests performance of writing entities in a <see cref="NativeStream"/> in an Entities.ForEach. </summary>
        /// <param name="entities"> Number of entities to write. </param>
        /// <param name="archetypes"> Number of archetypes to use. </param>
        [Test]
        [Performance]
        public void WriteEntitiesForEachNativeStream(
            [Values(10000, 1000000)] int entities,
            [Values(8, 256)] int archetypes)
        {
            var system = this.World.AddSystem(new EntitiesForEachTest(entities, archetypes));
            NativeStream stream = default;

            Measure.Method(() => system.NativeStreamTest(stream))
                   .SetUp(() => { stream = new NativeStream(entities, Allocator.TempJob); })
                   .CleanUp(() => stream.Dispose())
                   .Run();
        }

        /// <summary> Tests performance of writing entities in a <see cref="NativeQueue{T}"/> in an Entities.ForEach. </summary>
        /// <param name="entities"> Number of entities to write. </param>
        /// <param name="archetypes"> Number of archetypes to use. </param>
        [Test]
        [Performance]
        public void WriteEntitiesForEachNativeQueue(
            [Values(10000, 1000000)] int entities,
            [Values(8, 256)] int archetypes)
        {
            var system = this.World.AddSystem(new EntitiesForEachTest(entities, archetypes));
            NativeQueue<int> queue = default;

            Measure.Method(() => system.NativeQueueTest(queue))
                   .SetUp(() => { queue = new NativeQueue<int>(Allocator.TempJob); })
                   .CleanUp(() => queue.Dispose())
                   .Run();
        }

        /// <summary> Tests performance of reading entities parallel in a <see cref="NativeThreadStream"/>. </summary>
        /// <param name="entities"> Number of entities to write. </param>
        /// <param name="archetypes"> Number of archetypes to use. </param>
        [Test]
        [Performance]
        public void ReadParallelNativeThreadStream(
            [Values(10000, 1000000)] int entities,
            [Values(8, 256)] int archetypes)
        {
            var system = this.World.AddSystem(new EntitiesForEachTest(entities, archetypes));
            NativeThreadStream<int> stream = default;
            NativeQueue<int> output = default;

            Measure.Method(() =>
                {
                    new ReadNativeThreadStreamJob
                        {
                            Reader = stream.AsReader(),
                            Output = output.AsParallelWriter(),
                        }
                        .ScheduleParallel(stream.ForEachCount, 1, default).Complete();

                    Assert.AreEqual(entities, output.Count);
                })
                .SetUp(() =>
                {
                    stream = new NativeThreadStream<int>(Allocator.TempJob);
                    system.NativeThreadStreamTest(stream);

                    output = new NativeQueue<int>(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    stream.Dispose();
                    output.Dispose();
                })
                .Run();
        }

        /// <summary> Tests performance of reading entities parallel in a <see cref="NativeStream"/>. </summary>
        /// <param name="entities"> Number of entities to write. </param>
        /// <param name="archetypes"> Number of archetypes to use. </param>
        [Test]
        [Performance]
        public void ReadParallelNativeStream(
            [Values(10000, 1000000)] int entities,
            [Values(8, 256)] int archetypes)
        {
            var system = this.World.AddSystem(new EntitiesForEachTest(entities, archetypes));
            NativeStream stream = default;
            NativeQueue<int> output = default;

            Measure.Method(() =>
                {
                    new ReadNativeStreamJob
                        {
                            Reader = stream.AsReader(),
                            Output = output.AsParallelWriter(),
                        }
                        .ScheduleParallel(entities, 256, default).Complete();

                    Assert.AreEqual(entities, output.Count);
                })
                .SetUp(() =>
                {
                    stream = new NativeStream(entities, Allocator.TempJob);
                    system.NativeStreamTest(stream);

                    output = new NativeQueue<int>(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    stream.Dispose();
                    output.Dispose();
                })
                .Run();
        }

        /// <summary> Tests performance of reading entities in a <see cref="NativeQueue{T}"/>. </summary>
        /// <param name="entities"> Number of entities to write. </param>
        /// <param name="archetypes"> Number of archetypes to use. </param>
        [Test]
        [Performance]
        public void ReadNativeQueue(
            [Values(10000, 1000000)] int entities,
            [Values(8, 256)] int archetypes)
        {
            var system = this.World.AddSystem(new EntitiesForEachTest(entities, archetypes));
            NativeQueue<int> queue = default;
            NativeQueue<int> output = default;

            Measure.Method(() =>
                {
                    new ReadNativeQueueJob
                        {
                            Reader = queue,
                            Output = output.AsParallelWriter(),
                        }
                        .Schedule().Complete();

                    Assert.AreEqual(entities, output.Count);
                })
                .SetUp(() =>
                {
                    queue = new NativeQueue<int>(Allocator.TempJob);
                    system.NativeQueueTest(queue);

                    output = new NativeQueue<int>(Allocator.TempJob);
                })
                .CleanUp(() =>
                {
                    queue.Dispose();
                    output.Dispose();
                })
                .Run();
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct ReadNativeThreadStreamJob : IJobFor
        {
            [ReadOnly]
            public NativeThreadStream<int>.Reader Reader;

            public NativeQueue<int>.ParallelWriter Output;

            /// <inheritdoc/>
            public void Execute(int index)
            {
                var foreachCount = this.Reader.BeginForEachIndex(index);

                for (var i = 0; i < foreachCount; i++)
                {
                    this.Output.Enqueue(this.Reader.Read());
                }

                this.Reader.EndForEachIndex();
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct ReadNativeStreamJob : IJobFor
        {
            [ReadOnly]
            public NativeStream.Reader Reader;

            public NativeQueue<int>.ParallelWriter Output;

            /// <inheritdoc/>
            public void Execute(int index)
            {
                var foreachCount = this.Reader.BeginForEachIndex(index);

                for (var i = 0; i < foreachCount; i++)
                {
                    this.Output.Enqueue(this.Reader.Read<int>());
                }

                this.Reader.EndForEachIndex();
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct ReadNativeQueueJob : IJob
        {
            public NativeQueue<int> Reader;
            public NativeQueue<int>.ParallelWriter Output;

            /// <inheritdoc/>
            public void Execute()
            {
                while (this.Reader.TryDequeue(out var item))
                {
                    this.Output.Enqueue(item);
                }
            }
        }

        private class EntitiesForEachTest : SystemBase
        {
            private readonly int count;
            private readonly int archetypes;

            public EntitiesForEachTest(int count, int archetypes)
            {
                this.count = count;
                this.archetypes = archetypes;
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

                        this.EntityManager.SetSharedComponentData(entity, new TestComponent { Chunk = index % this.archetypes });
                    }
                }
            }

            protected override void OnUpdate()
            {
            }

            private struct TestComponent : ISharedComponentData
            {
                [UsedImplicitly]
                public int Chunk;
            }
        }
    }
}
#endif