// <copyright file="EventUtilityTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BL_TESTING

namespace BovineLabs.Event.Tests.Systems
{
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Entities.Tests;
    using Assert = UnityEngine.Assertions.Assert;

    /// <summary>
    /// The EventUtility.
    /// </summary>
    public class EventUtilityTests : ECSTestsFixture
    {
        /// <summary> Tests that capacity is set to number of events. </summary>
        [Test]
        public void EnsureHashMapCapacity()
        {
            const int startCount = 1;
            const int firstEventCount = 5;
            const int secondEventCount = 3;

            using (var hashmap = new NativeHashMap<int, int>(startCount, Allocator.TempJob))
            {
                for (var i = 0; i < startCount; i++)
                {
                    hashmap.Add(i, i);
                }

                var es = this.World.GetOrCreateSystem<TestEventSystem>();

                var events1 = es.CreateEventWriter<TestEvent>();

                // Write some event data
                for (var i = 0; i < firstEventCount; i++)
                {
                    events1.Write(i);
                }

                es.AddJobHandleForProducer<TestEvent>(default);

                var events2 = es.CreateEventWriter<TestEvent>();

                // Write some event data
                for (var i = 0; i < secondEventCount; i++)
                {
                    events2.Write(i);
                }

                es.AddJobHandleForProducer<TestEvent>(default);

                var handle = es.Ex<TestEvent>().EnsureHashMapCapacity(default, hashmap);
                handle.Complete();

                Assert.AreEqual(startCount + firstEventCount + secondEventCount, hashmap.Capacity);

                // Make sure it doesn't block getting readers
                es.GetEventReaders<TestEvent>(default, out _).Complete();
            }
        }

        /// <summary> Tests that capacity is set to number of events. </summary>
        [Test]
        public void GetEventCount()
        {
            const int firstEventCount = 5;
            const int secondEventCount = 3;

            using (var count = new NativeArray<int>(1, Allocator.TempJob))
            {
                var es = this.World.GetOrCreateSystem<TestEventSystem>();

                var events1 = es.CreateEventWriter<TestEvent>();

                // Write some event data
                for (var i = 0; i < firstEventCount; i++)
                {
                    events1.Write(i);
                }

                es.AddJobHandleForProducer<TestEvent>(default);

                var events2 = es.CreateEventWriter<TestEvent>();

                // Write some event data
                for (var i = 0; i < secondEventCount; i++)
                {
                    events2.Write(i);
                }

                es.AddJobHandleForProducer<TestEvent>(default);

                var handle = es.Ex<TestEvent>().GetEventCount(default, count);
                handle.Complete();

                Assert.AreEqual(firstEventCount + secondEventCount, count[0]);

                // Make sure it doesn't block getting readers
                es.GetEventReaders<TestEvent>(default, out _).Complete();
            }
        }

        [Test]
        public void ToNativeList()
        {
            const int firstEventCount = 5;
            const int secondEventCount = 3;

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            var events1 = es.CreateEventWriter<TestEvent>();

            // Write some event data
            for (var i = 0; i < firstEventCount; i++)
            {
                events1.Write(i);
            }

            es.AddJobHandleForProducer<TestEvent>(default);

            var events2 = es.CreateEventWriter<TestEvent>();

            // Write some event data
            for (var i = 0; i < secondEventCount; i++)
            {
                events2.Write(i);
            }

            es.AddJobHandleForProducer<TestEvent>(default);

            var handle = es.Ex<TestEvent>().ToNativeList(default, out var list);
            handle.Complete();

            Assert.AreEqual(firstEventCount + secondEventCount, list.Length);

            list.Dispose();

            // Make sure it doesn't block getting readers
            es.GetEventReaders<TestEvent>(default, out _).Complete();
        }
    }
}
#endif