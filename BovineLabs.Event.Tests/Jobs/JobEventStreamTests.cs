// <copyright file="JobEventTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BOVINELABS_TESTING_ENABLED

namespace BovineLabs.Event.Tests.Jobs
{
    using System;
    using BovineLabs.Event.Jobs;
    using BovineLabs.Event.Systems;
    using NUnit.Framework;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities.Tests;
    using Unity.Jobs;

    /// <summary> Tests for <see cref="JobEventStream"/>. </summary>
    public class JobEventStreamTests : ECSTestsFixture
    {
        /// <summary> Tests scheduling <see cref="JobEventStream.Schedule{TJob, T}"/> with parallel false. </summary>
        [Test]
        public void SeriesTest()
        {
            this.ScheduleTest((es, counter) => new TestJob
                {
                    Counter = counter.AsParallelWriter(),
                }
                .Schedule<TestJob, TestEvent>(es));
        }

        /// <summary> Tests scheduling <see cref="JobEventStream.Schedule{TJob, T}"/> with parallel true. </summary>
        [Test]
        public void ParallelTest()
        {
            this.ScheduleTest((es, counter) => new TestJob
                {
                    Counter = counter.AsParallelWriter(),
                }
                .Schedule<TestJob, TestEvent>(es, default, true));
        }

        private void ScheduleTest(Func<EventSystem, NativeQueue<int>, JobHandle> job)
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
                var finalHandle = job.Invoke(es, counter);

                finalHandle.Complete();

                Assert.AreEqual(producers, counter.Count);
            }
        }

        [BurstCompile]
        private struct TestJob : IJobEventStream<TestEvent>
        {
            public NativeQueue<int>.ParallelWriter Counter;

            public void Execute(NativeStream.Reader stream)
            {
                this.Counter.Enqueue(stream.ForEachCount);
            }
        }
    }
}

#endif