// <copyright file="EventSystemBase.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using System;
    using System.Collections.Generic;
    using BovineLabs.Event.Containers;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using UnityEngine.Assertions;
    using UnityEngine.Profiling;

    /// <summary>
    /// The base Event System class. Implement to add your own event system to a world or group.
    /// By default LateSimulation and Presentation are implemented.
    /// </summary>
    public abstract partial class EventSystemBase : SystemBase
    {
        // separate to avoid allocations when iterating
        private readonly List<EventContainer> containers = new List<EventContainer>();
        private readonly Dictionary<Type, int> types = new Dictionary<Type, int>();

        private StreamBus streamBus;

        /// <summary> The world to use that the event system is linked to. </summary>
        protected enum WorldMode
        {
            /// <summary> Uses the systems world, this.World </summary>
            WorldName,

            /// <summary> Uses the name "Default World" </summary>
            DefaultWorldName,

            /// <summary> Uses a custom world, this.Custom </summary>
            Custom,
        }

        /// <summary> Gets or sets a value indicating whether the persistent allocator should be used in stead of TempJob. </summary>
        public virtual bool UsePersistentAllocator { get; set; } = false;

        /// <summary> Gets the <see cref="WorldMode"/> of the system. </summary>
        // ReSharper disable once VirtualMemberNeverOverridden.Global
        protected virtual WorldMode Mode => WorldMode.WorldName;

        /// <summary> Gets the world when using <see cref="WorldMode.Custom"/> . </summary>
        // ReSharper disable once VirtualMemberNeverOverridden.Global
        protected virtual string CustomKey => throw new NotImplementedException("CustomKey must be implemented if Mode equals WorldMode.Custom");

        /// <summary> Create a new NativeEventStream in thread mode for writing events to. </summary>
        /// <typeparam name="T"> The type of event. </typeparam>
        /// <returns> A <see cref="NativeEventStream.ThreadWriter"/> you can write events to. </returns>
        /// <exception cref="InvalidOperationException"> Throw if unbalanced CreateEventWriter and AddJobHandleForProducer calls. </exception>
        public NativeEventStream.ThreadWriter CreateEventWriter<T>()
            where T : unmanaged
        {
            var container = this.GetOrCreateEventContainer<T>();
            return container.CreateEventStream(-1).AsThreadWriter();
        }

        /// <summary> Create a new NativeEventStream for writing events to . </summary>
        /// <param name="foreachCount"> The foreach count. </param>
        /// <typeparam name="T"> The type of event. </typeparam>
        /// <returns> A <see cref="NativeEventStream.IndexWriter"/> you can write events to. </returns>
        /// <exception cref="InvalidOperationException"> Throw if unbalanced CreateEventWriter and AddJobHandleForProducer calls. </exception>
        public NativeEventStream.IndexWriter CreateEventWriter<T>(int foreachCount)
            where T : unmanaged
        {
            Assert.IsFalse(foreachCount < 0);

            var container = this.GetOrCreateEventContainer<T>();
            return container.CreateEventStream(foreachCount).AsIndexWriter();
        }

        /// <summary> Adds the specified JobHandle to the events list of producer dependency handles. </summary>
        /// <param name="handle"> The job handle to add. </param>
        /// <typeparam name="T"> The type of event to associate the handle to. </typeparam>
        /// <exception cref="InvalidOperationException"> Throw if unbalanced CreateEventWriter and AddJobHandleForProducer calls. </exception>
        public void AddJobHandleForProducer<T>(JobHandle handle)
            where T : unmanaged
        {
            this.GetOrCreateEventContainer<T>().AddJobHandleForProducer(handle);
        }

        /// <summary> Checks if an event has any readers. </summary>
        /// <typeparam name="T"> The event type to check. </typeparam>
        /// <returns> True if there are readers for the event. </returns>
        public bool HasEventReaders<T>()
            where T : unmanaged
        {
            return this.GetEventReadersCount<T>() != 0;
        }

        /// <summary> Checks if an event has any readers. </summary>
        /// <typeparam name="T"> The event type to check. </typeparam>
        /// <returns> True if there are readers for the event. </returns>
        public int GetEventReadersCount<T>()
            where T : unmanaged
        {
            var container = this.GetOrCreateEventContainer<T>();
            return container.GetReadersCount();
        }

        /// <summary> Get the NativeEventStream for reading events from. </summary>
        /// <param name="handle"> Existing dependencies for this event. </param>
        /// <param name="readers"> A collection of <see cref="NativeEventStream.Reader"/> you can read events from. </param>
        /// <typeparam name="T"> The type of event. </typeparam>
        /// <returns> The updated dependency handle. </returns>
        public JobHandle GetEventReaders<T>(JobHandle handle, out IReadOnlyList<NativeEventStream.Reader> readers)
            where T : unmanaged
        {
            var container = this.GetOrCreateEventContainer<T>();
            readers = container.GetReaders();
            return JobHandle.CombineDependencies(container.ProducerHandle, handle);
        }

        /// <summary> Adds the specified JobHandle to the events list of consumer dependency handles. </summary>
        /// <param name="handle"> The job handle to add. </param>
        /// <typeparam name="T"> The type of event to associate the handle to. </typeparam>
        public void AddJobHandleForConsumer<T>(JobHandle handle)
            where T : unmanaged
        {
            this.GetOrCreateEventContainer<T>().AddJobHandleForConsumer(handle);
        }

        /// <summary> A collection of extension for events that avoid having to include long generics in their calls. </summary>
        /// <typeparam name="T"> The event type. </typeparam>
        /// <returns> The extensions container. </returns>
        public Extensions<T> Ex<T>()
            where T : unmanaged
        {
            return new Extensions<T>(this);
        }

        /// <summary> Adds readers from other event systems. </summary>
        /// <param name="type"> The type of event. </param>
        /// <param name="externalStreams"> Collection of event streams. </param>
        /// <param name="handle"> The dependency for the streams. </param>
        internal void AddExternalReaders(Type type, IReadOnlyList<NativeEventStream> externalStreams, JobHandle handle)
        {
            var container = this.GetOrCreateEventContainer(type);
            container.AddReaders(externalStreams);

            // updates producer handle because this is what consumers depend on
            container.AddJobHandleForProducerUnsafe(handle);
        }

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            var world = this.GetStreamKey();
            this.streamBus = StreamBus.GetInstance(world);
            this.streamBus.Subscribe(this);
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            var handles = new NativeArray<JobHandle>(this.containers.Count, Allocator.TempJob);

            for (var i = 0; i < this.containers.Count; i++)
            {
                var container = this.containers[i];

                // Need both handles because might have no writers or readers in this specific systems
                handles[i] = JobHandle.CombineDependencies(container.ConsumerHandle, container.ProducerHandle, container.DeferredProducerHandle);
                handles[i] = this.streamBus.ReleaseStreams(this, container.ExternalReaders, handles[i]);
                handles[i] = this.streamBus.ReleaseStreams(this, container.DeferredExternalReaders, handles[i]);

                container.Dispose();
            }

            JobHandle.CombineDependencies(handles).Complete();
            handles.Dispose();

            this.streamBus.Unsubscribe(this);
            this.containers.Clear();
            this.types.Clear();
        }

        /// <inheritdoc/>
        protected override void OnUpdate()
        {
            if (this.containers.Count == 0)
            {
                return;
            }

            var handles = new NativeArray<JobHandle>(this.containers.Count, Allocator.TempJob);

            for (var i = 0; i < this.containers.Count; i++)
            {
                var container = this.containers[i];

                // Need both handles because might have no writers or readers in this specific systems
                handles[i] = JobHandle.CombineDependencies(this.Dependency, container.ConsumerHandle, container.ProducerHandle);

                Profiler.BeginSample("ReleaseStreams");
                handles[i] = this.streamBus.ReleaseStreams(this, container.ExternalReaders, handles[i]);
                Profiler.EndSample();
                Profiler.BeginSample("AddStreams");
                handles[i] = this.streamBus.AddStreams(this, container.Type, container.Streams, handles[i]);
                Profiler.EndSample();

                container.Update();
            }

            this.Dependency = JobHandle.CombineDependencies(handles);
            handles.Dispose();
        }

        private string GetStreamKey()
        {
            switch (this.Mode)
            {
                case WorldMode.WorldName:
                    return this.World.Name;
                case WorldMode.DefaultWorldName:
                    return "Default World";
                case WorldMode.Custom:
                    return this.CustomKey;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private EventContainer GetOrCreateEventContainer<T>()
            where T : unmanaged
        {
            return this.GetOrCreateEventContainer(typeof(T));
        }

        private EventContainer GetOrCreateEventContainer(Type type)
        {
            if (!this.types.TryGetValue(type, out var index))
            {
                index = this.types[type] = this.containers.Count;
                this.containers.Add(new EventContainer(type, this.UsePersistentAllocator));
            }

            return this.containers[index];
        }
    }
}
