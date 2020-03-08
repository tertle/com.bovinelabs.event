// <copyright file="JobEventTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BOVINELABS_TESTING_ENABLED

namespace BovineLabs.Event.Tests.Jobs
{
    using BovineLabs.Event.Jobs;
    using NUnit.Framework;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities.Tests;
    using Unity.Jobs;

    /// <summary> Tests for <see cref="JobEvent"/>. </summary>
    public class JobEventTests : ECSTestsFixture
    {
        /// <summary> Tests that <see cref="JobEvent.ScheduleParallel{TJob, T}"/> schedules the job correctly. </summary>
        [Test]
        public void ScheduleParallel()
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
                    .ScheduleParallel<TestJob, TestEvent>(es, 64);

                finalHandle.Complete();

                Assert.AreEqual(foreachCount * eventCount * producers, counter.Count);
            }
        }

        [BurstCompile]
        private struct TestJob : IJobEvent<TestEvent>
        {
            public NativeQueue<int>.ParallelWriter Counter;

            public void Execute(TestEvent e)
            {
                this.Counter.Enqueue(e.Value);
            }
        }
    }
}

#endif