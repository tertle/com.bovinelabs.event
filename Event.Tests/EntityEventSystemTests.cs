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

            var queue = ees.CreateEventQueue<TestEvent1>();
            queue.Enqueue(default);

            ees.Update();

            var query = this.m_Manager.CreateEntityQuery(typeof(TestEvent1));
            Assert.AreEqual(1, query.CalculateLength());
        }

        [Test]
        public void CreateOneSize()
        {
            const int value = 3;

            var ees = this.World.GetOrCreateSystem<TestSystem>();

            var queue = ees.CreateEventQueue<TestEvent2>();
            queue.Enqueue(new TestEvent2 { Value = value });

            ees.Update();

            var query = this.m_Manager.CreateEntityQuery(typeof(TestEvent2));
            Assert.AreEqual(1, query.CalculateLength());
            Assert.AreEqual(value, query.GetSingleton<TestEvent2>().Value);
        }

        private struct TestEvent1 : IComponentData
        {

        }

        private struct TestEvent2 : IComponentData
        {
            public int Value;
        }

        private class TestSystem : EntityEventSystem
        {
        }
    }
}