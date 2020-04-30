// <copyright file="NativeThreadStreamTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Tests.Containers
{
    using System;
    using BovineLabs.Event.Containers;
    using NUnit.Framework;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;
    using Unity.Entities.Tests;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    /// <summary> Tests for <see cref="NativeThreadStream"/> . </summary>
    internal class NativeThreadStreamTests : ECSTestsFixture
    {
        /// <summary> Tests that you can create and destroy. </summary>
        [Test]
        public void CreateAndDestroy()
        {
            var stream = new NativeThreadStream<int>(Allocator.Temp);

            Assert.IsTrue(stream.IsCreated);
            Assert.IsTrue(stream.ComputeItemCount() == 0);

            stream.Dispose();
            Assert.IsFalse(stream.IsCreated);
        }

        /// <summary> Tests that the dispose job works. </summary>
        /// <remarks> The stream will be marked as not created straight away. </remarks>
        [Test]
        public void DisposeJob()
        {
            var stream = new NativeThreadStream<int>(Allocator.TempJob);
            Assert.IsTrue(stream.IsCreated);

            var fillInts = new WriteIntsJob { Writer = stream.AsWriter() };
            var writerJob = fillInts.Schedule(JobsUtility.MaxJobThreadCount, 16);

            var disposeJob = stream.Dispose(writerJob);
            Assert.IsFalse(stream.IsCreated);

            disposeJob.Complete();
        }

        /// <summary> Tests that ComputeItemCount() works. </summary>
        /// <param name="count"> <see cref="WriteIntsJob"/> count. </param>
        /// <param name="batchSize"> <see cref="WriteIntsJob"/> batch size. </param>
        [Test]
        public void ItemCount(
            [Values(1, 10, UnsafeThreadStream.ForEachCount + 1, 1024)] int count,
            [Values(1, 3, 10, 128)] int batchSize)
        {
            var stream = new NativeThreadStream<int>(Allocator.TempJob);
            var fillInts = new WriteIntsJob { Writer = stream.AsWriter() };
            fillInts.Schedule(count, batchSize).Complete();

            Assert.AreEqual(count * (count - 1) / 2, stream.ComputeItemCount());

            stream.Dispose();
        }

        /// <summary> Tests that writing from job then reading in multiple jobs works. </summary>
        /// <param name="count"> <see cref="WriteIntsJob"/> count. </param>
        /// <param name="batchSize"> <see cref="WriteIntsJob"/> batch size. </param>
        [Test]
        public void WriteRead(
            [Values(1, 10, UnsafeThreadStream.ForEachCount + 1)] int count,
            [Values(1, 3, 10)] int batchSize)
        {
            var stream = new NativeThreadStream<int>(Allocator.TempJob);
            var fillInts = new WriteIntsJob { Writer = stream.AsWriter() };
            var jobHandle = fillInts.Schedule(count, batchSize);

            var compareInts = new ReadIntsJob { Reader = stream.AsReader() };
            var res0 = compareInts.Schedule(stream.ForEachCount, batchSize, jobHandle);
            var res1 = compareInts.Schedule(stream.ForEachCount, batchSize, jobHandle);

            res0.Complete();
            res1.Complete();

            stream.Dispose();
        }

        /// <summary> Tests the container working in an Entities.ForEach in SystemBase. </summary>
        /// <param name="count"> The number of entities to test. </param>
        [Test]
        public void SystemBaseEntitiesForeach([Values(1, UnsafeThreadStream.ForEachCount + 1, 100000)] int count)
        {
            var system = this.World.AddSystem(new CodeGenTestSystem(count));
            system.Update();
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary> Ensures that reading with begin throws an exception. </summary>
        [Test]
        public void ReadWithoutBeginThrows()
        {
            var stream = new NativeThreadStream<int>(Allocator.Temp);
            stream.AsWriter().Write(0);

            var reader = stream.AsReader();
            Assert.Throws<ArgumentException>(() => reader.Read());

            stream.Dispose();
        }

        /// <summary> Ensures that begin reading out of range throws an exception. </summary>
        [Test]
        public void BeginOutOfRangeThrows()
        {
            var stream = new NativeThreadStream<int>(Allocator.Temp);

            var reader = stream.AsReader();
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.BeginForEachIndex(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                reader.BeginForEachIndex(UnsafeThreadStream.ForEachCount + 1));

            stream.Dispose();
        }

        /// <summary> Ensures reading past the end throws an exception. </summary>
        [Test]
        public void TooManyReadsThrows()
        {
            var stream = new NativeThreadStream<int>(Allocator.Temp);
            stream.AsWriter().Write(0);

            var reader = stream.AsReader();
            reader.BeginForEachIndex(0);
            reader.Read();
            Assert.Throws<ArgumentException>(() => reader.Read());

            stream.Dispose();
        }
#endif

        [BurstCompile(CompileSynchronously = true)]
        private struct WriteIntsJob : IJobParallelFor
        {
            public NativeThreadStream<int>.Writer Writer;

#pragma warning disable 649
            [NativeSetThreadIndex]
            private int threadIndex;
#pragma warning restore 649

            public void Execute(int index)
            {
                for (int i = 0; i != index; i++)
                {
                    this.Writer.Write(this.threadIndex);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct ReadIntsJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeThreadStream<int>.Reader Reader;

            public void Execute(int index)
            {
                int count = this.Reader.BeginForEachIndex(index);

                for (int i = 0; i != count; i++)
                {
                    var peekedValue = this.Reader.Peek();
                    var value = this.Reader.Read();

                    UnityEngine.Assertions.Assert.AreEqual(index, value);
                    UnityEngine.Assertions.Assert.AreEqual(index, peekedValue);
                }
            }
        }

        [DisableAutoCreation]
        private class CodeGenTestSystem : SystemBase
        {
            private readonly int count;
            private NativeHashMap<int, byte> hashmap;

            public CodeGenTestSystem(int count)
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
                var stream = new NativeThreadStream<int>(Allocator.TempJob);
                var writer = stream.AsWriter();

                this.Entities
                    .ForEach((in TestComponent test) => writer.Write(test.Value))
                    .ScheduleParallel();

                this.Dependency = new ReadJob
                    {
                        Reader = stream.AsReader(),
                        HashMap = this.hashmap.AsParallelWriter(),
                    }
                    .Schedule(stream.ForEachCount, 1, this.Dependency);

                this.Dependency = stream.Dispose(this.Dependency);
                this.Dependency.Complete();

                // Assert correct values were added
                for (var i = 0; i < this.count; i++)
                {
                    Assert.IsTrue(this.hashmap.TryGetValue(i, out _));
                }
            }

            [BurstCompile(CompileSynchronously = true)]
            private struct ReadJob : IJobParallelFor
            {
                [ReadOnly]
                public NativeThreadStream<int>.Reader Reader;

                public NativeHashMap<int, byte>.ParallelWriter HashMap;

                public void Execute(int index)
                {
                    int count = this.Reader.BeginForEachIndex(index);

                    for (int i = 0; i != count; i++)
                    {
                        var value = this.Reader.Read();
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
}