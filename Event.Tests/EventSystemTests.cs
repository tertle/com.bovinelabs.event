namespace BovineLabs.Event.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BovineLabs.Common.Tests;
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Jobs;

    /// <summary>
    /// The EventSystemTests.
    /// </summary>
    public class EventSystemTests : ECSTestsFixture
    {
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

        [Test]
        public void ProduceConsume()
        {
            int foreachCount = 1;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();
            var writer = es.CreateEventWriter<TestEvent>(foreachCount);

            writer.BeginForEachIndex(0);
            writer.Write(new TestEvent { Value = 3 });
            writer.Write(new TestEvent { Value = 4 });
            writer.EndForEachIndex();

            var handle = es.GetEventReaders<TestEvent>(default, out var readers);

            Assert.AreEqual(foreachCount, readers.Count);

            handle.Complete();

            var (reader, count) = readers[0];

            Assert.AreEqual(foreachCount, count);
            Assert.AreEqual(2, reader.BeginForEachIndex(0));
            Assert.AreEqual(3, reader.Read<TestEvent>().Value);
            Assert.AreEqual(4, reader.Read<TestEvent>().Value);
            reader.EndForEachIndex();
        }

        [Test]
        public void MultipleProducers()
        {
            int[] counts = { 2, 1, 3 };

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            foreach (var count in counts)
            {
                var writer1 = es.CreateEventWriter<TestEvent>(count);

                for (var i = 0; i < count; i++)
                {
                    writer1.BeginForEachIndex(i);
                    writer1.Write(new TestEvent { Value = i + 1 });
                    writer1.Write(new TestEvent { Value = i + 2 });
                    writer1.EndForEachIndex();
                }

                es.AddJobHandleForProducer<TestEvent>(default);
            }

            var handle = es.GetEventReaders<TestEvent>(default, out var readers);

            Assert.AreEqual(counts.Length, readers.Count);

            handle.Complete();

            for (var j = 0; j < readers.Count; j++)
            {
                var (reader, count) = readers[j];

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

        [Test]
        public void MultipleConsumers()
        {
            int foreachCount = 3;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            var writer1 = es.CreateEventWriter<TestEvent>(foreachCount);

            for (var i = 0; i < foreachCount; i++)
            {
                writer1.BeginForEachIndex(i);
                writer1.Write(new TestEvent { Value = i + 1 });
                writer1.Write(new TestEvent { Value = i + 2 });
                writer1.EndForEachIndex();
            }

            es.AddJobHandleForProducer<TestEvent>(default);

            var handle1 = es.GetEventReaders<TestEvent>(default, out var reader1);
            es.AddJobHandleForConsumer(handle1);
            var handle2 = es.GetEventReaders<TestEvent>(default, out var reader2);
            es.AddJobHandleForConsumer(handle2);

            // Just iterates both readers and checks them, as they should be identical.
            foreach (var readers in new List<IReadOnlyList<(NativeStream.Reader, int)>> { reader1, reader2 }.SelectMany(
                readers => readers))
            {
                var (reader, count) = readers;

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

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void CanNotWriteInReadMode()
        {
            var es = this.World.GetOrCreateSystem<TestEventSystem>();
            es.GetEventReaders<TestEvent>(default, out _);

            Assert.Throws<InvalidOperationException>(() => es.CreateEventWriter<TestEvent>(1));
            Assert.Throws<InvalidOperationException>(() => es.AddJobHandleForProducer<TestEvent>(default));
        }
#endif

        [Test]
        public void ProduceConsumeSim()
        {
            const int foreachCount = 100;

            const int producers = 2;
            const int consumers = 3;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            for (var i = 0; i < producers; i++)
            {
                var job1Handle = new ProducerJob
                    {
                        Events = es.CreateEventWriter<TestEvent>(foreachCount),
                    }
                    .Schedule(foreachCount, 8);

                es.AddJobHandleForProducer<TestEvent>(job1Handle);
            }

            JobHandle finalHandle = default;

            for (var i = 0; i < consumers; i++)
            {
                var handle = es.GetEventReaders<TestEvent>(default, out var readers);

                foreach (var (reader, count) in readers)
                {
                    handle = new ConsumerJob
                        {
                            Reader = reader,
                        }
                        .Schedule(count, 8, handle);
                }

                es.AddJobHandleForConsumer(handle);

                finalHandle = JobHandle.CombineDependencies(finalHandle, handle);
            }

            finalHandle.Complete();
        }

        private struct ProducerJob : IJobParallelFor
        {
            public NativeStream.Writer Events;

            /// <inheritdoc/>
            public void Execute(int index)
            {
                this.Events.BeginForEachIndex(index);
                for (var i = 0; i != 1000; i++)
                {
                    this.Events.Write(new TestEvent { Value = index + i });
                }

                this.Events.EndForEachIndex();
            }
        }

        private struct ConsumerJob : IJobParallelFor
        {
            public NativeStream.Reader Reader;

            /// <inheritdoc/>
            public void Execute(int index)
            {
                var count = this.Reader.BeginForEachIndex(index);

                for (var i = 0; i != count; i++)
                {
                    Assert.AreEqual(index + i, this.Reader.Read<TestEvent>().Value);
                }

                this.Reader.EndForEachIndex();
            }
        }

        private struct TestEvent
        {
            public int Value;
        }

        private class TestEventSystem : EventSystem
        {
        }
    }
}