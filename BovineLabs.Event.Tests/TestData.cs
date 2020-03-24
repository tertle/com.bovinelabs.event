// <copyright file="TestData.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>
// ReSharper disable SA1600

namespace BovineLabs.Event.Tests
{
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Systems;
    using NUnit.Framework;
    using Unity.Jobs;

    /// <summary> Test job for producing events. </summary>
    public struct ProducerJob : IJobParallelFor
    {
        /// <summary> The event stream writer. </summary>
        public NativeThreadStream.Writer Events;

        /// <summary> Number of events to write. </summary>
        public int EventCount;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            for (var i = 0; i != this.EventCount; i++)
            {
                this.Events.Write(new TestEvent { Value = i });
            }
        }
    }

    /// <summary> Test job for consuming events. </summary>
    public struct ConsumerJob : IJobParallelFor
    {
        /// <summary> The event stream reader. </summary>
        public NativeThreadStream.Reader Reader;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            var count = this.Reader.BeginForEachIndex(index);

            for (var i = 0; i != count; i++)
            {
                this.Reader.Read<TestEvent>();
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