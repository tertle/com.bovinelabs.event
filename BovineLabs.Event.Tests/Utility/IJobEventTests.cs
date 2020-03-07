namespace BovineLabs.Event.Tests
{
    using BovineLabs.Event.Utility;
    using NUnit.Framework;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities.Tests;
    using Unity.Jobs;

    public class IJobEventTests : ECSTestsFixture
    {
        [Test]
        public void ScheduleParallelSplit()
        {
            const int foreachCount = 100;
            const int eventCount = 100;
            const int producers = 2;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            for (var i = 0; i < producers; i++)
            {
                var writer = es.CreateEventWriter<TestEvent>(foreachCount);

                var handle = new ProducerJob
                    {
                        Events = writer,
                        EventCount = eventCount,
                    }
                    .Schedule(foreachCount, 8);

                es.AddJobHandleForProducer<TestEvent>(handle);
            }

            using (var counter = new NativeQueue<int>(Allocator.TempJob))
            {
                var finalHandle = new TestJob
                    {
                        Counter = counter.AsParallelWriter(),
                    }
                    .ScheduleParallel<TestJob, TestEvent>(es, 64, default);

                finalHandle.Complete();

                Assert.AreEqual(foreachCount * eventCount * producers, counter.Count);
            }
        }

        [BurstCompile]
        private struct TestJob : IJobEvent<TestEvent>
        {
            public NativeQueue<int>.ParallelWriter Counter;

            public void Execute(TestEvent value)
            {
                this.Counter.Enqueue(value.Value);
            }
        }
    }
}