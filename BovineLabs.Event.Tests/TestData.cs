// <copyright file="TestData.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>
// ReSharper disable SA1600

namespace BovineLabs.Event.Tests
{
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Jobs;

    /// <summary> Test job for producing events. </summary>
    public struct ProducerJob : IJobParallelFor
    {
        /// <summary> The event stream writer. </summary>
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

    /// <summary> Test job for consuming events. </summary>
    public struct ConsumerJob : IJobParallelFor
    {
        /// <summary> The event stream reader. </summary>
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

    /// <summary> Test event. </summary>
    public struct TestEvent
    {
        /// <summary> The event value. </summary>
        public int Value;
    }

    /// <summary> Test event system. </summary>
    public class TestEventSystem : EventSystem
    {
    }

    /// <summary> A second test event system. </summary>
    public class TestEventSystem2 : EventSystem
    {
    }
}