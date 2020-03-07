// <copyright file="TestData.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Tests
{
    using System.Diagnostics.CodeAnalysis;
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Jobs;

    // ReSharper disable once SA1649
    [SuppressMessage("ReSharper", "SA1649")]
    public struct ProducerJob : IJobParallelFor
    {
        public NativeStream.Writer Events;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            this.Events.BeginForEachIndex(index);
            for (var i = 0; i != 100; i++)
            {
                this.Events.Write(new TestEvent { Value = index + i });
            }

            this.Events.EndForEachIndex();
        }
    }

    public struct ConsumerJob : IJobParallelFor
    {
        public NativeStream.Reader Reader;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            var count = this.Reader.BeginForEachIndex(index);

            for (var i = 0; i != count; i++)
            {
                Assert.AreEqual(index + i, this.Reader.Read<TestEvent>().Value);
            }

            this.Reader.EndForEachIndex();
        }
    }

    public struct TestEvent
    {
        public int Value;
    }

    public class TestEventSystem : EventSystem
    {
    }

    public class TestEventSystem2 : EventSystem
    {
    }

    public class CustomErrorTestEventSystem : EventSystem
    {
        protected override WorldMode Mode => WorldMode.Custom;
    }

    public class CustomTestEventSystem : EventSystem
    {
        protected override WorldMode Mode => WorldMode.Custom;

        protected override string CustomKey => "test";
    }

    public class WorldModeUnknownTestEventSystem : EventSystem
    {
        protected override WorldMode Mode => (WorldMode)123;
    }

    public class WorldModeActiveTestEventSystem : EventSystem
    {
        protected override WorldMode Mode => WorldMode.DefaultWorldName;
    }
}