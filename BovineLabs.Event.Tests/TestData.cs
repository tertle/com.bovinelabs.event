// <copyright file="TestData.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>
// ReSharper disable SA1600

#if BL_TESTING

namespace BovineLabs.Event.Tests
{
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Jobs;
    using BovineLabs.Event.Systems;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary> Test job for producing events. </summary>
    public struct ProducerJob : IJobFor
    {
        /// <summary> The event stream writer. </summary>
        public NativeEventStream.IndexWriter Events;

        /// <summary> Number of events to write. </summary>
        public int EventCount;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            this.Events.BeginForEachIndex(index);
            for (var i = 0; i != this.EventCount; i++)
            {
                this.Events.Write(new TestEvent { Value = i });
            }

            this.Events.EndForEachIndex();
        }
    }

    /// <summary> Test job for consuming events. </summary>
    public struct ConsumerJob : IJobFor
    {
        /// <summary> The event stream reader. </summary>
        public NativeEventStream.Reader Reader;

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

    /// <summary> Test job for consuming events. </summary>
    public struct ConsumerEventJob : IJobEvent<TestEvent>
    {
        /// <inheritdoc/>
        public void Execute(TestEvent e)
        {
        }
    }

    /// <summary> Test event. </summary>
    public struct TestEvent
    {
        /// <summary> The event value. </summary>
        public int Value;
    }

    /// <summary> A test component with a value. </summary>
    public struct TestComponent : IComponentData
    {
        /// <summary> Gets or sets a test value. </summary>
        public int Value;
    }

    /// <summary> Test event system. </summary>
    public class TestEventSystem : EventSystemBase
    {
    }

    /// <summary> A second test event system. </summary>
    public class TestEventSystem2 : EventSystemBase
    {
    }
}

#endif