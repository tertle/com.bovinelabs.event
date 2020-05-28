// <copyright file="JobEventStreamTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BOVINELABS_TESTING_ENABLED

namespace BovineLabs.Event.Tests.Jobs
{
    using System;
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Jobs;
    using BovineLabs.Event.Systems;
    using NUnit.Framework;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities.Tests;
    using Unity.Jobs;

    /// <summary> Tests for <see cref="JobEventStream"/> . </summary>
    public class JobEventStreamTests : ECSTestsFixture
    {
        /// <summary> Tests scheduling <see cref="JobEventStream.Schedule{TJob,T}"/> with parallel false. </summary>
        [Test]
        public void Series()
        {
            this.ScheduleTest((es, counter) => new TestJob
                {
                    Counter = counter.AsParallelWriter(),
                }
                .Schedule<TestJob, TestEvent>(es));
        }

        /// <summary> Tests scheduling <see cref="JobEventStream.Schedule{TJob,T}"/> with parallel true. </summary>
        [Test]
        public void Simultaneous()
        {
            this.ScheduleTest((es, counter) => new TestJob
                {
                    Counter = counter.AsParallelWriter(),
                }
                .ScheduleSimultaneous<TestJob, TestEvent>(es));
        }

        private void ScheduleTest(Func<EventSystemBase, NativeQueue<int>, JobHandle> job)
        {
            const int foreachCount = 100;
            const int eventCount = 100;
            const int producers = 2;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            for (var i = 0; i < producers; i++)
            {
                var writer = es.CreateEventWriter<TestEvent>();

                var handle = new ProducerJob
                    {
                        Events = writer,
                        EventCount = eventCount,
                    }
                    .ScheduleParallel(foreachCount, 8, default);

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
            [NativeDisableContainerSafetyRestriction]
            public NativeQueue<int>.ParallelWriter Counter;

            public void Execute(NativeThreadStream.Reader stream, int index)
            {
                this.Counter.Enqueue(stream.ForEachCount);
            }
        }
    }
}

#endif