// <copyright file="JobEventTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BL_TESTING

namespace BovineLabs.Event.Tests.Jobs
{
    using BovineLabs.Event.Jobs;
    using NUnit.Framework;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities.Tests;
    using Unity.Jobs;

    /// <summary> Tests for <see cref="JobEvent"/> . </summary>
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

            JobHandle handle = default;

            for (var i = 0; i < producers; i++)
            {
                var writer = es.CreateEventWriter<TestEvent>(foreachCount);

                var defaultHandle = new ProducerJob
                    {
                        Events = writer,
                        EventCount = eventCount,
                    }
                    .ScheduleParallel(foreachCount, 8, default);

                es.AddJobHandleForProducer<TestEvent>(handle);
                handle = JobHandle.CombineDependencies(handle, defaultHandle);
            }

            using (var counter = new NativeQueue<int>(Allocator.TempJob))
            {
                var finalHandle = new ParallelTestJob
                    {
                        Counter = counter.AsParallelWriter(),
                    }
                    .ScheduleParallel<ParallelTestJob, TestEvent>(es, handle);

                finalHandle.Complete();

                Assert.AreEqual(foreachCount * eventCount * producers, counter.Count);
            }
        }

        /// <summary> Tests that <see cref="JobEvent.ScheduleParallel{TJob, T}"/> schedules the job correctly. </summary>
        [Test]
        public void Schedule()
        {
            const int foreachCount = 100;
            const int eventCount = 100;
            const int producers = 2;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            JobHandle handle = default;

            for (var i = 0; i < producers; i++)
            {
                var writer = es.CreateEventWriter<TestEvent>(foreachCount);

                var defaultHandle = new ProducerJob
                    {
                        Events = writer,
                        EventCount = eventCount,
                    }
                    .ScheduleParallel(foreachCount, 8, default);

                es.AddJobHandleForProducer<TestEvent>(handle);
                handle = JobHandle.CombineDependencies(handle, defaultHandle);
            }

            using (var counter = new NativeQueue<int>(Allocator.TempJob))
            {
                var finalHandle = new SingleTestJob
                    {
                        Counter = counter,
                    }
                    .Schedule<SingleTestJob, TestEvent>(es, handle);

                finalHandle.Complete();

                Assert.AreEqual(foreachCount * eventCount * producers, counter.Count);
            }
        }

        [BurstCompile]
        private struct ParallelTestJob : IJobEvent<TestEvent>
        {
            public NativeQueue<int>.ParallelWriter Counter;

            public void Execute(TestEvent e)
            {
                this.Counter.Enqueue(e.Value);
            }
        }

        [BurstCompile]
        private struct SingleTestJob : IJobEvent<TestEvent>
        {
            public NativeQueue<int> Counter;

            public void Execute(TestEvent e)
            {
                this.Counter.Enqueue(e.Value);
            }
        }
    }
}

#endif