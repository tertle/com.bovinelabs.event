// <copyright file="EventUtilityTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Tests
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
        [Test]
        public void EnsureHashMapCapacity()
        {
            const int startCount = 1;
            const int firstEventCount = 5;
            const int secondEventCount = 3;

            var hashmap = new NativeHashMap<int, int>(startCount, Allocator.TempJob);
            for (var i = 0; i < startCount; i++)
            {
                hashmap.Add(i, i);
            }

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            var events1 = es.CreateEventWriter<TestEvent>(10);

            // Write some event data
            events1.BeginForEachIndex(0);
            for (var i = 0; i < firstEventCount; i++)
            {
                events1.Write(i);
            }

            events1.EndForEachIndex();
            es.AddJobHandleForProducer<TestEvent>(default);

            var events2 = es.CreateEventWriter<TestEvent>(5);

            // Write some event data
            events2.BeginForEachIndex(4);
            for (var i = 0; i < secondEventCount; i++)
            {
                events2.Write(i);
            }

            events2.EndForEachIndex();
            es.AddJobHandleForProducer<TestEvent>(default);

            var handle = es.EnsureHashMapCapacity<TestEvent, int, int>(default, hashmap);
            handle.Complete();

            Assert.AreEqual(startCount + firstEventCount + secondEventCount, hashmap.Capacity);

            // Make sure it doesn't block getting readers
            es.GetEventReaders<TestEvent>(default, out _).Complete();
        }
    }
}