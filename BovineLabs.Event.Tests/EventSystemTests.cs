// <copyright file="EventSystemTests.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BOVINELABS_TESTING_ENABLED

namespace BovineLabs.Event.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BovineLabs.Event.Data;
    using BovineLabs.Event.Systems;
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Entities.Tests;
    using Unity.Jobs;

    /// <summary>
    /// The EventSystemTests.
    /// </summary>
    public class EventSystemTests : ECSTestsFixture
    {
        /// <summary> Testing CreateEventWriter calls must be paired with a AddJobHandleForProducer call. </summary>
        [Test]
        public void CreateEventWriterAddJobHandleForProducerMustBePaired()
        {
            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            es.CreateEventWriter<TestEvent>(1);
            Assert.Throws<InvalidOperationException>(() => es.CreateEventWriter<TestEvent>(1));

            es.AddJobHandleForProducer<TestEvent>(default);

            Assert.Throws<InvalidOperationException>(() => es.AddJobHandleForProducer<TestEvent>(default));

            Assert.DoesNotThrow(() => es.CreateEventWriter<TestEvent>(1));
        }

        /// <summary> Validates the count input of CreateEventWriter. </summary>
        [Test]
        public void CreateEventWriterMustHaveAValidCount()
        {
            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            Assert.Throws<ArgumentException>(() => es.CreateEventWriter<TestEvent>(0));
            Assert.Throws<ArgumentException>(() => es.CreateEventWriter<TestEvent>(-1));
            Assert.DoesNotThrow(() => es.CreateEventWriter<TestEvent>(1));
        }

        /// <summary> Testing GetEventReaders calls must be paired with a AddJobHandleForConsumer call. </summary>
        [Test]
        public void GetEventReadersAddJobHandleForConsumerMustBePaired()
        {
            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            es.GetEventReaders<TestEvent>(default, out _);
            Assert.Throws<InvalidOperationException>(() => es.GetEventReaders<TestEvent>(default, out _));

            es.AddJobHandleForConsumer<TestEvent>(default);

            Assert.Throws<InvalidOperationException>(() => es.AddJobHandleForConsumer<TestEvent>(default));

            Assert.DoesNotThrow(() => es.GetEventReaders<TestEvent>(default, out _));
        }

        /// <summary> Ensures that <see cref="EventSystem.WorldMode.Custom"/> requires CustomWorld to be implemented. </summary>
        [Test]
        public void WorldModeCustomRequiresCustomWorldImplementation()
        {
            Assert.Throws<NotImplementedException>(() => this.World.GetOrCreateSystem<CustomErrorTestEventSystem>());
            Assert.DoesNotThrow(() => this.World.GetOrCreateSystem<CustomTestEventSystem>());
        }

        /// <summary> Checks that <see cref="EventSystem.WorldMode"/> unknown throws an ArgumentOutOfRangeException. </summary>
        [Test]
        public void WorldModeUnknownThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                this.World.GetOrCreateSystem<WorldModeUnknownTestEventSystem>());
        }

        /// <summary> Checks that <see cref="EventSystem.WorldMode.DefaultWorldName"/> does not throw an exception. </summary>
        /// <remarks> Need a way to actually test this better. </remarks>
        [Test]
        public void WorldModeActiveNoException()
        {
            Assert.DoesNotThrow(() =>
                this.World.GetOrCreateSystem<WorldModeActiveTestEventSystem>());
        }

        /// <summary> Test producing and consuming. </summary>
        [Test]
        public void ProduceConsume()
        {
            const int foreachCount = 1;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();
            var writer = es.CreateEventWriter<TestEvent>(foreachCount);

            writer.BeginForEachIndex(0);
            writer.Write(new TestEvent { Value = 3 });
            writer.Write(new TestEvent { Value = 4 });
            writer.EndForEachIndex();

            var handle = es.GetEventReaders<TestEvent>(default, out var readers);

            Assert.AreEqual(foreachCount, readers.Length);

            handle.Complete();

            var r = readers[0];
            var reader = r.Item1.AsReader();
            var count = r.Item2;

            Assert.AreEqual(foreachCount, count);
            Assert.AreEqual(2, reader.BeginForEachIndex(0));
            Assert.AreEqual(3, reader.Read<TestEvent>().Value);
            Assert.AreEqual(4, reader.Read<TestEvent>().Value);
            reader.EndForEachIndex();
        }

        /// <summary> Test multiple producers for 1 consumer. </summary>
        [Test]
        public void MultipleProducers()
        {
            int[] counts = { 2, 1, 3 };

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            foreach (var count in counts)
            {
                var writer = es.CreateEventWriter<TestEvent>(count);

                for (var i = 0; i < count; i++)
                {
                    writer.BeginForEachIndex(i);
                    writer.Write(new TestEvent { Value = i + 1 });
                    writer.Write(new TestEvent { Value = i + 2 });
                    writer.EndForEachIndex();
                }

                es.AddJobHandleForProducer<TestEvent>(default);
            }

            var handle = es.GetEventReaders<TestEvent>(default, out var readers);

            Assert.AreEqual(counts.Length, readers.Length);

            handle.Complete();

            for (var j = 0; j < readers.Length; j++)
            {
                var r = readers[j];
                var reader = r.Item1.AsReader();
                var count = r.Item2;

                Assert.AreEqual(counts[j], count);
                Assert.AreEqual(counts[j], reader.ForEachCount);

                for (var i = 0; i < count; i++)
                {
                    Assert.AreEqual(2, reader.BeginForEachIndex(i));
                    Assert.AreEqual(i + 1, reader.Read<TestEvent>().Value);
                    Assert.AreEqual(i + 2, reader.Read<TestEvent>().Value);
                    reader.EndForEachIndex();
                }
            }
        }

        /// <summary> Test multiple consumer for 1 producer. </summary>
        [Test]
        public void MultipleConsumers()
        {
            int foreachCount = 3;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            var writer = es.CreateEventWriter<TestEvent>(foreachCount);

            for (var i = 0; i < foreachCount; i++)
            {
                writer.BeginForEachIndex(i);
                writer.Write(new TestEvent { Value = i + 1 });
                writer.Write(new TestEvent { Value = i + 2 });
                writer.EndForEachIndex();
            }

            es.AddJobHandleForProducer<TestEvent>(default);

            var handle1 = es.GetEventReaders<TestEvent>(default, out var reader1);
            es.AddJobHandleForConsumer<TestEvent>(handle1);
            var handle2 = es.GetEventReaders<TestEvent>(default, out var reader2);
            es.AddJobHandleForConsumer<TestEvent>(handle2);

            // Just iterates both readers and checks them, as they should be identical.
            foreach (var readers in new List<NativeArray<ValueTuple<NativeStreamImposter.Reader, int>>> { reader1, reader2 }.SelectMany(
                readers => readers))
            {
                var reader = readers.Item1.AsReader();
                var count = readers.Item2;

                Assert.AreEqual(foreachCount, count);
                Assert.AreEqual(foreachCount, reader.ForEachCount);

                for (var i = 0; i < count; i++)
                {
                    Assert.AreEqual(2, reader.BeginForEachIndex(i));
                    Assert.AreEqual(i + 1, reader.Read<TestEvent>().Value);
                    Assert.AreEqual(i + 2, reader.Read<TestEvent>().Value);
                    reader.EndForEachIndex();
                }
            }
        }

        /// <summary> Test multiple producer, consumer with job simulation. </summary>
        [Test]
        public void ProduceConsumeJobSimulation()
        {
            const int foreachCount = 100;

            const int producers = 2;
            const int consumers = 3;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            for (var i = 0; i < producers; i++)
            {
                var writer = es.CreateEventWriter<TestEvent>(foreachCount);

                var handle = new ProducerJob
                    {
                        Events = writer,
                        EventCount = 100,
                    }
                    .Schedule(foreachCount, 8);

                es.AddJobHandleForProducer<TestEvent>(handle);
            }

            JobHandle finalHandle = default;

            for (var i = 0; i < consumers; i++)
            {
                var handle = es.GetEventReaders<TestEvent>(default, out var readers);

                foreach (var r in readers)
                {
                    var reader = r.Item1;
                    var count = r.Item2;

                    handle = new ConsumerJob
                        {
                            Reader = reader,
                        }
                        .Schedule(count, 8, handle);
                }

                es.AddJobHandleForConsumer<TestEvent>(handle);

                finalHandle = JobHandle.CombineDependencies(finalHandle, handle);
            }

            finalHandle.Complete();
        }

        /// <summary> Tests multiple event systems with different update rates. </summary>
        [Test]
        public void DifferentUpdateRate()
        {
            const int foreachCount = 3;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();
            var es2 = this.World.GetOrCreateSystem<TestEventSystem2>();

            var writer = es.CreateEventWriter<TestEvent>(foreachCount);

            for (var i = 0; i < foreachCount; i++)
            {
                writer.BeginForEachIndex(i);
                writer.Write(new TestEvent { Value = i + 1 });
                writer.Write(new TestEvent { Value = i + 2 });
                writer.EndForEachIndex();
            }

            es.AddJobHandleForProducer<TestEvent>(default);

            es.Update();

            var writer2 = es.CreateEventWriter<TestEvent>(foreachCount);

            for (var i = 0; i < foreachCount; i++)
            {
                writer2.BeginForEachIndex(i);
                writer2.Write(new TestEvent { Value = i + 1 + 1 });
                writer2.Write(new TestEvent { Value = i + 2 + 1 });
                writer2.EndForEachIndex();
            }

            es.AddJobHandleForProducer<TestEvent>(default);

            es.Update();

            var handle = es2.GetEventReaders<TestEvent>(default, out var readers);

            Assert.AreEqual(2, readers.Length);

            handle.Complete();

            for (var j = 0; j < readers.Length; j++)
            {
                var r = readers[j];
                var reader = r.Item1.AsReader();
                var count = r.Item2;

                Assert.AreEqual(foreachCount, count);
                Assert.AreEqual(foreachCount, reader.ForEachCount);

                for (var i = 0; i < count; i++)
                {
                    Assert.AreEqual(2, reader.BeginForEachIndex(i));
                    Assert.AreEqual(i + 1 + j, reader.Read<TestEvent>().Value);
                    Assert.AreEqual(i + 2 + j, reader.Read<TestEvent>().Value);
                    reader.EndForEachIndex();
                }
            }

            es2.Update(); // releases streams
        }

        /// <summary> Ensures writing in read mode throws exception. </summary>
        [Test]
        public void WriteInReadModeThrowsInvalidOperationException()
        {
            var es = this.World.GetOrCreateSystem<TestEventSystem>();
            es.GetEventReaders<TestEvent>(default, out _);

            Assert.Throws<InvalidOperationException>(
                () => es.CreateEventWriter<TestEvent>(1));

            Assert.Throws<InvalidOperationException>(
                () => es.AddJobHandleForProducer<TestEvent>(default));
        }

        /// <summary> Tests event readers returns correctly. </summary>
        [Test]
        public void HasEventReaders()
        {
            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            // No writer should return false
            Assert.IsFalse(es.HasEventReaders<TestEvent>());

            // Update to reset
            es.Update();

            // Only writers that are empty should also return false
            es.CreateEventWriter<TestEvent>(1);
            es.AddJobHandleForProducer<TestEvent>(default);

            Assert.IsTrue(es.HasEventReaders<TestEvent>());
        }

        private class CustomErrorTestEventSystem : EventSystem
        {
            protected override WorldMode Mode => WorldMode.Custom;
        }

        private class CustomTestEventSystem : EventSystem
        {
            protected override WorldMode Mode => WorldMode.Custom;

            protected override string CustomKey => "test";
        }

        private class WorldModeUnknownTestEventSystem : EventSystem
        {
            protected override WorldMode Mode => (WorldMode)123;
        }

        private class WorldModeActiveTestEventSystem : EventSystem
        {
            protected override WorldMode Mode => WorldMode.DefaultWorldName;
        }
    }
}

#endif