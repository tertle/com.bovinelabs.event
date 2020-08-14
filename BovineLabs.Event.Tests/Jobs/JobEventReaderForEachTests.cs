// <copyright file="JobEventReaderForEachTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BL_TESTING

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

    /// <summary> Tests for <see cref="JobEventReader"/> . </summary>
    public class JobEventReaderForEachTests : ECSTestsFixture
    {
        /// <summary> Tests scheduling <see cref="JobEventReaderForEach.ScheduleParallel{TJob,T}"/>. </summary>
        [Test]
        public void ScheduleParallel()
        {
            this.ScheduleTest((es, counter) => new TestJob
                {
                    Counter = counter.AsParallelWriter(),
                }
                .ScheduleParallel<TestJob, TestEvent>(es));
        }

        private void ScheduleTest(Func<EventSystemBase, NativeQueue<int>, JobHandle> job)
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
                    .ScheduleParallel(foreachCount, 8, default);

                es.AddJobHandleForProducer<TestEvent>(handle);
            }

            using (var counter = new NativeQueue<int>(Allocator.TempJob))
            {
                var finalHandle = job.Invoke(es, counter);

                finalHandle.Complete();

                Assert.AreEqual(foreachCount * producers, counter.Count);
            }
        }

        [BurstCompile]
        private struct TestJob : IJobEventReaderForEach<TestEvent>
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeQueue<int>.ParallelWriter Counter;

            public void Execute(NativeEventStream.Reader reader, int foreachIndex)
            {
                var count = reader.BeginForEachIndex(foreachIndex);
                this.Counter.Enqueue(count);
            }
        }
    }
}

#endif