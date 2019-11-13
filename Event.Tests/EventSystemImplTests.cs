namespace BovineLabs.Event.Tests
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Jobs;

    /// <summary>
    /// The EventSystemImplTests.
    /// </summary>
    public class EventSystemImplTests
    {
        [Test]
        public void CreateEventWriterAddJobHandleForProducerMustBePaired()
        {
            var es = new EventSystemImpl();

            es.CreateEventWriter<TestEvent>();
            Assert.Throws<InvalidOperationException>(() => es.CreateEventWriter<TestEvent>());

            es.AddJobHandleForProducer<TestEvent>(default);

            Assert.Throws<InvalidOperationException>(() => es.AddJobHandleForProducer<TestEvent>(default));

            Assert.DoesNotThrow(() => es.CreateEventWriter<TestEvent>());

            es.Dispose();
        }

        [Test]
        public void ProduceConsume()
        {
            var es = new EventSystemImpl();
            var writer = es.CreateEventWriter<TestEvent>();

            writer.Enqueue(new TestEvent { Value = 3 });
            writer.Enqueue(new TestEvent { Value = 4 });

            var handle = es.GetEventReader<TestEvent>(default, out var reader);

            Assert.AreEqual(1, reader.ForEachCount);

            handle.Complete();

            Assert.AreEqual(2, reader.BeginForEachIndex(0));
            Assert.AreEqual(3, reader.Read<TestEvent>().Value);
            Assert.AreEqual(4, reader.Read<TestEvent>().Value);
            reader.EndForEachIndex();

            es.Dispose();
        }

        [Test]
        public void MultipleProducers()
        {
            var es = new EventSystemImpl();

            var writer1 = es.CreateEventWriter<TestEvent>();
            es.AddJobHandleForProducer<TestEvent>(default);
            writer1.Enqueue(new TestEvent { Value = 3 });
            writer1.Enqueue(new TestEvent { Value = 4 });

            var writer2 = es.CreateEventWriter<TestEvent>();
            es.AddJobHandleForProducer<TestEvent>(default);
            writer2.Enqueue(new TestEvent { Value = 5 });

            var handle = es.GetEventReader<TestEvent>(default, out var reader);

            Assert.AreEqual(2, reader.ForEachCount);

            handle.Complete();

            Assert.AreEqual(2, reader.BeginForEachIndex(0));
            Assert.AreEqual(3, reader.Read<TestEvent>().Value);
            Assert.AreEqual(4, reader.Read<TestEvent>().Value);
            reader.EndForEachIndex();

            Assert.AreEqual(1, reader.BeginForEachIndex(1));
            Assert.AreEqual(5, reader.Read<TestEvent>().Value);
            reader.EndForEachIndex();

            es.Dispose();
        }

        [Test]
        public void MultipleConsumers()
        {
            var es = new EventSystemImpl();

            var writer = es.CreateEventWriter<TestEvent>();
            es.AddJobHandleForProducer<TestEvent>(default);
            writer.Enqueue(new TestEvent { Value = 3 });
            writer.Enqueue(new TestEvent { Value = 4 });

            var handle = es.GetEventReader<TestEvent>(default, out var reader1);
            handle = es.GetEventReader<TestEvent>(handle, out var reader2);

            Assert.AreEqual(1, reader1.ForEachCount);
            Assert.AreEqual(1, reader2.ForEachCount);

            handle.Complete();

            Assert.AreEqual(2, reader1.BeginForEachIndex(0));
            Assert.AreEqual(3, reader1.Read<TestEvent>().Value);
            Assert.AreEqual(4, reader1.Read<TestEvent>().Value);
            reader1.EndForEachIndex();

            Assert.AreEqual(2, reader2.BeginForEachIndex(0));
            Assert.AreEqual(3, reader2.Read<TestEvent>().Value);
            Assert.AreEqual(4, reader2.Read<TestEvent>().Value);
            reader1.EndForEachIndex();

            es.Dispose();
        }

        [Test]
        public void CanNotWriteInReadMode()
        {
            var es = new EventSystemImpl();
            es.GetEventReader<TestEvent>(default, out _);

            Assert.Throws<InvalidOperationException>(() => es.CreateEventWriter<TestEvent>());
            Assert.Throws<InvalidOperationException>(() => es.AddJobHandleForProducer<TestEvent>(default));

            es.Dispose();
        }

        [Test]
        public void ProduceConsumeSim()
        {
            int count1 = 20;
            int count2 = 12;

            var es = new EventSystemImpl();

            var job1Handle = new ProducerJob
                {
                    Events = es.CreateEventWriter<TestEvent>().AsParallelWriter(),
                }
                .Schedule(count1, 5);

            es.AddJobHandleForProducer<TestEvent>(job1Handle);

            var job2Handle = new ProducerJob
                {
                    Events = es.CreateEventWriter<TestEvent>().AsParallelWriter(),
                }
                .Schedule(count2, 5);

            es.AddJobHandleForProducer<TestEvent>(job2Handle);

            JobHandle handle = default;
            handle = es.GetEventReader<TestEvent>(handle, out var reader);

            handle = new ConsumerJob
                {
                    Reader = reader,
                }
                .Schedule(reader.ForEachCount, 1, handle);

            handle.Complete();
        }

        public struct ProducerJob : IJobParallelFor
        {
            public NativeQueue<TestEvent>.ParallelWriter Events;

            public void Execute(int index)
            {
                this.Events.Enqueue(new TestEvent { Value = index });
            }
        }

        public struct ConsumerJob : IJobParallelFor
        {
            public NativeStream.Reader Reader;

            public void Execute(int index)
            {
                var count = Reader.BeginForEachIndex(index);

                Assert.IsTrue(count == 20 || count == 12);

                // this.Reader.EndForEachIndex();
            }
        }

        public struct TestEvent
        {
            public int Value;
        }
    }
}