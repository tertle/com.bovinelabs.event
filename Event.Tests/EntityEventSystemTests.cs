// <copyright file="EntityEventSystemTests.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Tests
{
    using BovineLabs.Common.Tests;
    using NUnit.Framework;
    using Unity.Entities;

    public class EntityEventSystemTests : ECSTestsFixture
    {
        [Test]
        public void CreateNoSize()
        {
            var ees = this.World.GetOrCreateSystem<TestSystem>();

            var queue = ees.CreateEventQueue<TestEmptyEvent>();
            queue.Enqueue(default);

            ees.Update();

            var query = this.m_Manager.CreateEntityQuery(typeof(TestEmptyEvent));
            Assert.AreEqual(1, query.CalculateEntityCount());
        }

        [Test]
        public void CreateOneSize()
        {
            const int value = 3;

            var ees = this.World.GetOrCreateSystem<TestSystem>();

            var queue = ees.CreateEventQueue<TestEvent>();
            queue.Enqueue(new TestEvent { Value = value });

            ees.Update();

            var query = this.m_Manager.CreateEntityQuery(typeof(TestEvent));
            Assert.AreEqual(1, query.CalculateEntityCount());
            Assert.AreEqual(value, query.GetSingleton<TestEvent>().Value);
        }

        private struct TestEmptyEvent : IComponentData
        {
        }

        private struct TestEvent : IComponentData
        {
            public int Value;
        }

        private class TestSystem : EntityEventSystem
        {
        }
    }
}