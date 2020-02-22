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
            var hashmap = new NativeHashMap<int, int>(1, Allocator.TempJob);
            hashmap.Add(1, 1);

            var es = this.World.GetOrCreateSystem<TestEventSystem>();

            var events1 = es.CreateEventWriter<TestEvent>(10);

            // Write some event data
            events1.BeginForEachIndex(0);
            for (var i = 0; i < 5; i++)
            {
                events1.Write(i);
            }

            events1.EndForEachIndex();
            es.AddJobHandleForProducer<TestEvent>(default);

            var events2 = es.CreateEventWriter<TestEvent>(5);

            // Write some event data
            events2.BeginForEachIndex(4);
            for (var i = 0; i < 3; i++)
            {
                events2.Write(i);
            }

            events2.EndForEachIndex();
            es.AddJobHandleForProducer<TestEvent>(default);

            var handle = es.EnsureHashMapCapacity<TestEvent, int, int>(default, hashmap);
            handle.Complete();

            Assert.AreEqual(9, hashmap.Capacity);
        }
    }
}