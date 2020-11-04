// <copyright file="EventSystemTests.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BL_TESTING

namespace BovineLabs.Event.Tests.Systems
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Jobs;
    using BovineLabs.Event.Systems;
    using NUnit.Framework;
    using Unity.Entities.Tests;
    using Unity.Jobs;

    /// <summary> Tests for <see cref="EventSystemBase"/> . </summary>
    public class EventSystemTests : ECSTestsFixture
    {
        /// <summary> Testing CreateEventWriter calls must be paired with a AddJobHandleForProducer call. </summary>
        [Test]
        public void CreateEventWriterAddJobHandleForProducerMustBePaired()
        {
            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            es.CreateEventWriter<TestEvent>();
            Assert.Throws<InvalidOperationException>(() => es.CreateEventWriter<TestEvent>());

            es.AddJobHandleForProducer<TestEvent>(default);

            Assert.Throws<InvalidOperationException>(() => es.AddJobHandleForProducer<TestEvent>(default));

            Assert.DoesNotThrow(() => es.CreateEventWriter<TestEvent>());
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

        /// <summary> Ensures that <see cref="EventSystemBase.WorldMode.Custom"/> requires CustomWorld to be implemented. </summary>
        [Test]
        public void WorldModeCustomRequiresCustomWorldImplementation()
        {
            Assert.Throws<NotImplementedException>(() => this.World.GetOrCreateSystem<CustomErrorTestEventSystem>());
            Assert.DoesNotThrow(() => this.World.GetOrCreateSystem<CustomTestEventSystem>());
        }

        /// <summary> Checks that <see cref="EventSystemBase.WorldMode"/> unknown throws an ArgumentOutOfRangeException. </summary>
        [Test]
        public void WorldModeUnknownThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                this.World.GetOrCreateSystem<WorldModeUnknownTestEventSystem>());
        }

        /// <summary> Checks that <see cref="EventSystemBase.WorldMode.DefaultWorldName"/> does not throw an exception. </summary>
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
            var es = this.World.GetOrCreateSystem<TestEventSystem>();
            var writer = es.CreateEventWriter<TestEvent>();

            writer.Write(new TestEvent { Value = 3 });
            writer.Write(new TestEvent { Value = 4 });

            var handle = es.GetEventReaders<TestEvent>(default, out var readers);

            handle.Complete();

            var r = readers[0];

            Assert.AreEqual(2, r.BeginForEachIndex(0));
            Assert.AreEqual(3, r.Read<TestEvent>().Value);
            Assert.AreEqual(4, r.Read<TestEvent>().Value);
            r.EndForEachIndex();
        }

        /// <summary> Test multiple producers for 1 consumer. </summary>
        [Test]
        public void MultipleProducers()
        {
            int[] counts = { 2, 1, 3 };

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            foreach (var count in counts)
            {
                var writer = es.CreateEventWriter<TestEvent>();

                for (var i = 0; i < count; i++)
                {
                    writer.Write(new TestEvent { Value = i + 1 });
                }

                es.AddJobHandleForProducer<TestEvent>(default);
            }

            var handle = es.GetEventReaders<TestEvent>(default, out var readers);

            Assert.AreEqual(counts.Length, readers.Count);

            handle.Complete();

            for (var i = 0; i < readers.Count; i++)
            {
                var r = readers[i];

                Assert.AreEqual(counts[i], r.BeginForEachIndex(0));
                for (var j = 0; j < counts[i]; j++)
                {
                    Assert.AreEqual(j + 1, r.Read<TestEvent>().Value);
                }

                r.EndForEachIndex();
            }
        }

        /// <summary> Test multiple consumer for 1 producer. </summary>
        [Test]
        public void MultipleConsumers()
        {
            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            var writer = es.CreateEventWriter<TestEvent>();

            writer.Write(new TestEvent { Value = 1 });
            writer.Write(new TestEvent { Value = 2 });

            es.AddJobHandleForProducer<TestEvent>(default);

            var handle1 = es.GetEventReaders<TestEvent>(default, out var reader1);
            es.AddJobHandleForConsumer<TestEvent>(handle1);
            var handle2 = es.GetEventReaders<TestEvent>(default, out var reader2);
            es.AddJobHandleForConsumer<TestEvent>(handle2);

            // Just iterates both readers and checks them, as they should be identical.
            foreach (var reader in new List<IReadOnlyList<NativeEventStream.Reader>> { reader1, reader2 }.SelectMany(
                readers => readers))
            {
                Assert.AreEqual(2, reader.BeginForEachIndex(0));
                Assert.AreEqual(1, reader.Read<TestEvent>().Value);
                Assert.AreEqual(2, reader.Read<TestEvent>().Value);
                reader.EndForEachIndex();
            }
        }

        /// <summary> Test multiple producer, consumer with job simulation. </summary>
        [Test]
        public void ProduceConsumeJobSimulation()
        {
            const int foreachCount = 100;
            const int eventCount = 100;

            const int producers = 2;
            const int consumers = 3;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            for (var i = 0; i < producers; i++)
            {
                var writer = es.CreateEventWriter<TestEvent>(foreachCount);

                var handle = new ProducerJob
                    {
                        Events = writer,
                        EventCount = eventCount,
                    }
                    .ScheduleParallel(foreachCount, 8, default);

                es.AddJobHandleForProducer<TestEvent>(handle);
            }

            JobHandle finalHandle = default;

            for (var i = 0; i < consumers; i++)
            {
                var handle = es.GetEventReaders<TestEvent>(default, out var readers);

                foreach (var reader in readers)
                {
                    handle = new ConsumerJob
                        {
                            Reader = reader,
                        }
                        .ScheduleParallel(reader.ForEachCount, 8, handle);
                }

                es.AddJobHandleForConsumer<TestEvent>(handle);

                finalHandle = JobHandle.CombineDependencies(finalHandle, handle);
            }

            finalHandle.Complete();

            es.GetEventReaders<TestEvent>(default, out var r);
            Assert.AreEqual(foreachCount * eventCount * producers, r.Select(s => s.Count()).Sum());
        }

        /// <summary> Tests multiple event systems with different update rates. </summary>
        [Test]
        public void DifferentUpdateRate()
        {
            var es = this.World.GetOrCreateSystem<TestEventSystem>();
            var es2 = this.World.GetOrCreateSystem<TestEventSystem2>();

            var writer = es.CreateEventWriter<TestEvent>();

            writer.Write(new TestEvent { Value = 1 });
            writer.Write(new TestEvent { Value = 2 });

            es.AddJobHandleForProducer<TestEvent>(default);

            es.Update();

            var writer2 = es.CreateEventWriter<TestEvent>();

            writer2.Write(new TestEvent { Value = 1 + 1 });
            writer2.Write(new TestEvent { Value = 2 + 1 });

            es.AddJobHandleForProducer<TestEvent>(default);

            es.Update();

            var handle = es2.GetEventReaders<TestEvent>(default, out var readers);

            Assert.AreEqual(2, readers.Count);

            handle.Complete();

            for (var j = 0; j < readers.Count; j++)
            {
                var reader = readers[j];

                Assert.AreEqual(2, reader.BeginForEachIndex(0));
                Assert.AreEqual(1 + j, reader.Read<TestEvent>().Value);
                Assert.AreEqual(2 + j, reader.Read<TestEvent>().Value);
                reader.EndForEachIndex();
            }

            es2.Update(); // releases streams
        }

        /// <summary> Ensures writing in read mode throws exception. </summary>
        [Test]
        public void WriteInReadModeAddsToDeferred()
        {
            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            // GetEventReadersCount won't create the container so just do it with a dummy GetEventReaders
            es.GetEventReaders<TestEvent>(default, out _);
            es.AddJobHandleForConsumer<TestEvent>(default);

            Assert.AreEqual(0, es.GetEventReadersCount<TestEvent>());

            es.CreateEventWriter<TestEvent>();

            // Still deferred
            Assert.AreEqual(0, es.GetEventReadersCount<TestEvent>());

            es.Update();

            // Returned to read queue.
            Assert.AreEqual(1, es.GetEventReadersCount<TestEvent>());
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
            es.CreateEventWriter<TestEvent>();
            es.AddJobHandleForProducer<TestEvent>(default);

            Assert.IsTrue(es.HasEventReaders<TestEvent>());
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary> Tests no safety is throw from different ordering. </summary>
        [Test]
        public void OrderSafetyTest()
        {
            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            var writer1 = es.CreateEventWriter<TestEvent>(64);

            var producer1 = new ProducerJob
                {
                    EventCount = 10,
                    Events = writer1,
                }
                .ScheduleParallel(64, 1, default);

            es.AddJobHandleForProducer<TestEvent>(producer1);
            var writer2 = es.CreateEventWriter<TestEvent>(64);

            var producer2 = new ProducerJob
                {
                    EventCount = 10,
                    Events = writer2,
                }
                .ScheduleParallel(64, 1, default);

            es.AddJobHandleForProducer<TestEvent>(producer2);

            var consumer1 = default(ConsumerEventJob).ScheduleParallel<ConsumerEventJob, TestEvent>(es);
            var consumer2 = default(ConsumerEventJob).ScheduleParallel<ConsumerEventJob, TestEvent>(es);

            var writer3 = es.CreateEventWriter<TestEvent>(64);

            var producer3 = new ProducerJob
                {
                    EventCount = 10,
                    Events = writer3,
                }
                .ScheduleParallel(64, 1, default);

            es.AddJobHandleForProducer<TestEvent>(producer3);

            var consumer3 = default(ConsumerEventJob).ScheduleParallel<ConsumerEventJob, TestEvent>(es);

            es.Update();

            var consumer4 = default(ConsumerEventJob).ScheduleParallel<ConsumerEventJob, TestEvent>(es);

            var writer4 = es.CreateEventWriter<TestEvent>(64);
            var producer4 = new ProducerJob
                {
                    EventCount = 10,
                    Events = writer4,
                }
                .ScheduleParallel(64, 1, default);

            es.AddJobHandleForProducer<TestEvent>(producer4);

            var consumer5 = default(ConsumerEventJob).ScheduleParallel<ConsumerEventJob, TestEvent>(es);

            consumer5.Complete();
            consumer4.Complete();
            consumer1.Complete();
            consumer2.Complete();
            consumer3.Complete();
        }
#endif

        private class CustomErrorTestEventSystem : EventSystemBase
        {
            protected override WorldMode Mode => WorldMode.Custom;
        }

        private class CustomTestEventSystem : EventSystemBase
        {
            protected override WorldMode Mode => WorldMode.Custom;

            protected override string CustomKey => "test";
        }

        private class WorldModeUnknownTestEventSystem : EventSystemBase
        {
            protected override WorldMode Mode => (WorldMode)123;
        }

        private class WorldModeActiveTestEventSystem : EventSystemBase
        {
            protected override WorldMode Mode => WorldMode.DefaultWorldName;
        }
    }
}

#endif