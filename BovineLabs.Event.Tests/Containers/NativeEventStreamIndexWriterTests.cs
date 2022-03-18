// <copyright file="NativeEventStreamIndexWriterTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BL_TESTING

namespace BovineLabs.Event.Tests.Containers
{
    using BovineLabs.Event.Containers;
    using BovineLabs.Testing;
    using NUnit.Framework;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

        internal class IndexWriter : ECSTestsFixture
        {
            private const int ForEachCount = 16;

            /// <summary> Tests that the dispose job works. </summary>
            /// <remarks> The stream will be marked as not created straight away. </remarks>
            [Test]
            public void DisposeJob()
            {
                var stream = new NativeEventStream(ForEachCount, Allocator.TempJob);
                Assert.IsTrue(stream.IsCreated);

                var fillInts = new WriteIntsJob { Writer = stream.AsIndexWriter() };
                var writerJob = fillInts.ScheduleParallel(ForEachCount, 16, default);

                var disposeJob = stream.Dispose(writerJob);
                Assert.IsFalse(stream.IsCreated);

                disposeJob.Complete();
            }

            /// <summary> Tests that ComputeItemCount() works. </summary>
            /// <param name="foreachCount"> <see cref="WriteIntsJob"/> count. </param>
            /// <param name="batchSize"> <see cref="WriteIntsJob"/> batch size. </param>
            [Test]
            public void ItemCount(
                [Values(1, 2, JobsUtility.MaxJobThreadCount + 1, 1024)]
                int foreachCount,
                [Values(1, 3, 10, 128)] int batchSize)
            {
                var stream = new NativeEventStream(foreachCount, Allocator.TempJob);
                var fillInts = new WriteIntsJob { Writer = stream.AsIndexWriter() };
                fillInts.ScheduleParallel(foreachCount, batchSize, default).Complete();

                Assert.AreEqual(foreachCount * (foreachCount - 1) / 2, stream.Count());

                stream.Dispose();
            }

            /// <summary> Tests that writing from job then reading in multiple jobs works. </summary>
            /// <param name="foreachCount"> The foreach count. </param>
            /// <param name="batchSize"> <see cref="WriteIntsJob"/> batch size. </param>
            [Test]
            public void WriteRead(
                [Values(1, 10, JobsUtility.MaxJobThreadCount + 1)]
                int foreachCount,
                [Values(1, 3, 10)] int batchSize)
            {
                var stream = new NativeEventStream(foreachCount, Allocator.TempJob);
                var fillInts = new WriteIntsJob { Writer = stream.AsIndexWriter() };
                var jobHandle = fillInts.ScheduleParallel(foreachCount, batchSize, default);

                var compareInts = new ReadIntsJob { JobReader = stream.AsReader() };
                var res0 = compareInts.ScheduleParallel(stream.ForEachCount, batchSize, jobHandle);
                var res1 = compareInts.ScheduleParallel(stream.ForEachCount, batchSize, jobHandle);

                res0.Complete();
                res1.Complete();

                stream.Dispose();
            }

            /// <summary> Tests the container working in an Entities.ForEach in SystemBase. </summary>
            /// <param name="count"> The number of entities to test. </param>
            [Test]
            public void SystemBaseEntitiesForeach([Values(1, JobsUtility.MaxJobThreadCount + 1, 100000)]
                int count)
            {
                var system = this.World.AddSystem(new IndexWriterCodeGenTestSystem(count));
                system.Update();
            }

            [BurstCompile(CompileSynchronously = true)]
            private struct WriteIntsJob : IJobFor
            {
                public NativeEventStream.IndexWriter Writer;

                public void Execute(int index)
                {
                    this.Writer.BeginForEachIndex(index);
                    for (int i = 0; i != index; i++)
                    {
                        this.Writer.Write(index);
                    }

                    this.Writer.EndForEachIndex();
                }
            }

            [BurstCompile(CompileSynchronously = true)]
            private struct ReadIntsJob : IJobFor
            {
                [ReadOnly]
                public NativeEventStream.Reader JobReader;

                public void Execute(int index)
                {
                    int count = this.JobReader.BeginForEachIndex(index);

                    for (int i = 0; i != count; i++)
                    {
                        var peekedValue = this.JobReader.Peek<int>();
                        var value = this.JobReader.Read<int>();

                        UnityEngine.Assertions.Assert.AreEqual(index, value);
                        UnityEngine.Assertions.Assert.AreEqual(index, peekedValue);
                    }
                }
            }
        }


            [DisableAutoCreation]
            internal partial class IndexWriterCodeGenTestSystem : SystemBase
            {
                private readonly int count;
                private NativeHashMap<int, byte> hashmap;
                private EntityQuery query;

                public IndexWriterCodeGenTestSystem(int count)
                {
                    this.count = count;
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

                            this.EntityManager.SetComponentData(entity, new TestComponent { Value = index });
                        }
                    }

                    this.hashmap = new NativeHashMap<int, byte>(this.count, Allocator.Persistent);
                }

                protected override void OnDestroy()
                {
                    this.hashmap.Dispose();
                }

                protected override void OnUpdate()
                {
                    this.EntitiesForEach();
                    this.JobWithCode();
                }

                private void EntitiesForEach()
                {
                    var stream = new NativeEventStream(this.query.CalculateEntityCount(), Allocator.TempJob);
                    var writer = stream.AsIndexWriter();

                    this.Entities
                        .ForEach((int entityInQueryIndex, in TestComponent test) =>
                        {
                            writer.BeginForEachIndex(entityInQueryIndex);
                            writer.Write(test.Value);
                            writer.EndForEachIndex();
                        })
                        .WithStoreEntityQueryInField(ref query)
                        .ScheduleParallel();

                    this.Dependency = new ReadJob
                        {
                            JobReader = stream.AsReader(),
                            HashMap = this.hashmap.AsParallelWriter(),
                        }
                        .ScheduleParallel(stream.ForEachCount, 1, this.Dependency);

                    this.Dependency = stream.Dispose(this.Dependency);
                    this.Dependency.Complete();

                    // Assert correct values were added
                    for (var i = 0; i < this.count; i++)
                    {
                        Assert.IsTrue(this.hashmap.TryGetValue(i, out _));
                    }

                    this.hashmap.Clear();
                }

                private void JobWithCode()
                {
                    var stream = new NativeEventStream(1, Allocator.TempJob);
                    var writer = stream.AsIndexWriter();

                    var c = this.count;

                    this.Job.WithCode(() =>
                        {
                            writer.BeginForEachIndex(0);

                            for (var i = 0; i < c; i++)
                            {
                                writer.Write(i);
                            }

                            writer.EndForEachIndex();
                        })
                        .Schedule();

                    this.Dependency = new ReadJob
                        {
                            JobReader = stream.AsReader(),
                            HashMap = this.hashmap.AsParallelWriter(),
                        }
                        .ScheduleParallel(stream.ForEachCount, 1, this.Dependency);

                    this.Dependency = stream.Dispose(this.Dependency);
                    this.Dependency.Complete();

                    // Assert correct values were added
                    for (var i = 0; i < this.count; i++)
                    {
                        Assert.IsTrue(this.hashmap.TryGetValue(i, out _));
                    }

                    this.hashmap.Clear();
                }

                [BurstCompile(CompileSynchronously = true)]
                private struct ReadJob : IJobFor
                {
                    [ReadOnly]
                    public NativeEventStream.Reader JobReader;

                    public NativeHashMap<int, byte>.ParallelWriter HashMap;

                    public void Execute(int index)
                    {
                        int count = this.JobReader.BeginForEachIndex(index);

                        for (int i = 0; i != count; i++)
                        {
                            var value = this.JobReader.Read<int>();
                            this.HashMap.TryAdd(value, 0);
                        }
                    }
                }

                private struct TestComponent : IComponentData
                {
                    public int Value;
                }
            }
}

#endif
