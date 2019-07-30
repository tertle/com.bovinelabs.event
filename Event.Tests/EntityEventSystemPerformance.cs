// <copyright file="EntityEventSystemPerformance.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Tests
{
    using BovineLabs.Common.Tests;
    using JetBrains.Annotations;
    using NUnit.Framework;
    using Unity.Entities;
    using Unity.PerformanceTesting;

    /// <summary>
    /// The EntityEventSystemPerformance.
    /// </summary>
    public class EntityEventSystemPerformance : ECSTestsFixture
    {
        /// <summary>
        /// Tests the performance of creating empty events.
        /// </summary>
        /// <param name="count">Number of events to create.</param>
        [TestCase(100000)]
        [Performance]
        public void CreateEmptyEvents(int count)
        {
            var ees = this.World.GetOrCreateSystem<EndSimulationEntityEventSystem>();

            Measure.Method(ees.Update)
                .SetUp(() =>
                {
                    var queue = ees.CreateEventQueue<TestEmptyEvent>();

                    for (var i = 0; i < count; i++)
                    {
                        queue.Enqueue(default);
                    }
                })
                .Run();
        }

        /// <summary>
        /// Tests the performance of creating events.
        /// </summary>
        /// <param name="count">Number of events to create.</param>
        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        [Performance]
        public void CreateEvents(int count)
        {
            var ees = this.World.GetOrCreateSystem<EndSimulationEntityEventSystem>();

            Measure.Method(ees.Update)
                .SetUp(() =>
                {
                    var queue = ees.CreateEventQueue<TestEvent>();

                    for (var i = 0; i < count; i++)
                    {
                        queue.Enqueue(default);
                    }
                })
                .Run();
        }

        /// <summary>
        /// Tests the performance of creating events from different queues.
        /// </summary>
        /// <param name="count">Number of events to create.</param>
        [TestCase(100)]
        [TestCase(100000)]
        [Performance]
        public void CreateEvents5Queues(int count)
        {
            var ees = this.World.GetOrCreateSystem<EndSimulationEntityEventSystem>();

            count = count / 5;

            Measure.Method(ees.Update)
                .SetUp(() =>
                {
                    for (var j = 0; j < 5; j++)
                    {
                        var queue = ees.CreateEventQueue<TestEvent>();
                        for (var i = 0; i < count; i++)
                        {
                            queue.Enqueue(default);
                        }
                    }
                })
                .Run();
        }

        /// <summary>
        /// Tests the performance of creating different events.
        /// </summary>
        /// <param name="count">Number of events to create.</param>
        [TestCase(100)]
        [TestCase(100000)]
        [Performance]
        public void CreateEvents5Types(int count)
        {
            var ees = this.World.GetOrCreateSystem<EndSimulationEntityEventSystem>();

            count = count / 5;

            Measure.Method(ees.Update)
                .SetUp(() =>
                {
                    var queue = ees.CreateEventQueue<TestEvent>();
                    for (var i = 0; i < count; i++)
                    {
                        queue.Enqueue(new TestEvent { Value = i });
                    }

                    var queue1 = ees.CreateEventQueue<TestEvent1>();
                    for (var i = 0; i < count; i++)
                    {
                        queue1.Enqueue(new TestEvent1 { Value = i });
                    }

                    var queue2 = ees.CreateEventQueue<TestEvent2>();
                    for (var i = 0; i < count; i++)
                    {
                        queue2.Enqueue(new TestEvent2 { Value = i });
                    }

                    var queue3 = ees.CreateEventQueue<TestEvent3>();
                    for (var i = 0; i < count; i++)
                    {
                        queue3.Enqueue(new TestEvent3 { Value = i });
                    }

                    var queue4 = ees.CreateEventQueue<TestEvent4>();
                    for (var i = 0; i < count; i++)
                    {
                        queue4.Enqueue(new TestEvent4 { Value = i });
                    }
                })
                .Run();
        }

        /// <summary>
        /// Tests the performance of destroying events.
        /// </summary>
        /// <param name="count">Number of events to destroy.</param>
        [TestCase(100000)]
        [Performance]
        public void DestroyEvents(int count)
        {
            var ees = this.World.GetOrCreateSystem<EndSimulationEntityEventSystem>();

            Measure.Method(ees.Update)
                .SetUp(() =>
                {
                    var queue = ees.CreateEventQueue<TestEmptyEvent>();

                    for (var i = 0; i < count; i++)
                    {
                        queue.Enqueue(default);
                    }

                    ees.Update();
                })
                .Run();
        }

        private struct TestEmptyEvent : IComponentData
        {
        }

        private struct TestEvent : IComponentData
        {
            [UsedImplicitly]
            public int Value;
        }

        private struct TestEvent1 : IComponentData
        {
            [UsedImplicitly]
            public int Value;
        }

        private struct TestEvent2 : IComponentData
        {
            [UsedImplicitly]
            public int Value;
        }

        private struct TestEvent3 : IComponentData
        {
            [UsedImplicitly]
            public int Value;
        }

        private struct TestEvent4 : IComponentData
        {
            [UsedImplicitly]
            public int Value;
        }
    }
}