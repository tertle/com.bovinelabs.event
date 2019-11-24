// <copyright file="EventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using UnityEngine.Profiling;

    /// <summary>
    /// The base Event System class. Implement to add your own event system to a world or group.
    /// By default LateSimulation and Presentation are implemented.
    /// </summary>
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach", Justification = "Unity.")]
    public abstract class EventSystem : JobComponentSystem
    {
        private const string worldNotImplemented = "WorldMode.Custom requires Custom to be implemented.";

        // separate to avoid allocations when iterating
        private readonly List<EventContainer> containers = new List<EventContainer>();
        private readonly Dictionary<Type, int> types = new Dictionary<Type, int>();

        private StreamBus streamBus;

        /// <summary>
        /// The world to use that the event system is linked to.
        /// </summary>
        protected enum WorldMode
        {
            /// <summary>
            /// Uses the systems world, this.World
            /// </summary>
            WorldName,

            /// <summary>
            /// Uses the name "Default World"
            /// </summary>
            DefaultWorldName,

            /// <summary>
            /// Uses a custom world, this.Custom
            /// </summary>
            Custom,
        }

        /// <summary>
        /// Gets the . Override to change the sharing state of the
        /// </summary>
        // ReSharper disable once VirtualMemberNeverOverridden.Global
        protected virtual WorldMode Mode => WorldMode.WorldName;

        /// <summary>
        /// Gets the world when using <see cref="WorldMode.Custom"/>.
        /// </summary>
        // ReSharper disable once VirtualMemberNeverOverridden.Global
        protected virtual string CustomKey => throw new NotImplementedException("CustomKey must be implemented if Mode equals WorldMode.Custom");

        /// <summary>
        /// Create a new NativeStream for writing events to.
        /// </summary>
        /// <param name="foreachCount">The <see cref="NativeStream.ForEachCount"/>.</param>
        /// <typeparam name="T">The type of event.</typeparam>
        /// <returns>A <see cref="NativeStream.Writer"/> you can write events to.</returns>
        /// <exception cref="InvalidOperationException">Throw if unbalanced CreateEventWriter and AddJobHandleForProducer calls.</exception>
        public NativeStream.Writer CreateEventWriter<T>(int foreachCount)
            where T : struct
        {
            var container = this.GetOrCreateEventContainer<T>();

            return container.CreateEventStream(foreachCount);
        }

        /// <summary>
        /// Adds the specified JobHandle to the events list of producer dependency handles.
        /// </summary>
        /// <param name="handle">The job handle to add.</param>
        /// <typeparam name="T">The type of event to associate the handle to.</typeparam>
        /// <exception cref="InvalidOperationException">Throw if unbalanced CreateEventWriter and AddJobHandleForProducer calls.</exception>
        public void AddJobHandleForProducer<T>(JobHandle handle)
            where T : struct
        {
            this.GetOrCreateEventContainer<T>().AddJobHandleForProducer(handle);
        }

        /// <summary>
        /// Get the NativeStream for reading events from.
        /// </summary>
        /// <param name="handle">Existing dependencies for this event.</param>
        /// <param name="readers">A collection of <see cref="NativeStream.Reader"/> you can read events from.</param>
        /// <typeparam name="T">The type of event.</typeparam>
        /// <returns>The updated dependency handle.</returns>
        /// <exception cref="InvalidOperationException">Throw if unbalanced CreateEventWriter and AddJobHandleForProducer calls.</exception>
        public JobHandle GetEventReaders<T>(JobHandle handle, out IReadOnlyList<Tuple<NativeStream.Reader, int>> readers)
            where T : struct
        {
            var container = this.GetOrCreateEventContainer<T>();

            if (!container.ReadMode)
            {
                container.SetReadMode();
            }

            readers = container.GetReaders();

            return JobHandle.CombineDependencies(container.ProducerHandle, handle);
        }

        /// <summary>
        /// Adds the specified JobHandle to the events list of consumer dependency handles.
        /// </summary>
        /// <param name="handle">The job handle to add.</param>
        /// <typeparam name="T">The type of event to associate the handle to.</typeparam>
        /// <exception cref="InvalidOperationException">Throw if unbalanced GetEventReaders and AddJobHandleForConsumer calls.</exception>
        public void AddJobHandleForConsumer<T>(JobHandle handle)
            where T : struct
        {
            this.GetOrCreateEventContainer<T>().AddJobHandleForConsumer(handle);
        }

        /// <summary>
        /// Adds readers from other event systems.
        /// </summary>
        /// <param name="type">The type of event.</param>
        /// <param name="externalStreams">Collection of event streams.</param>
        /// <param name="handle">The dependency for the streams.</param>
        internal void AddExternalReaders(Type type, IReadOnlyList<Tuple<NativeStream, int>> externalStreams, JobHandle handle)
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
                handles[i] = JobHandle.CombineDependencies(container.ConsumerHandle, container.ProducerHandle);
                handles[i] = this.streamBus.ReleaseStreams(this, container.ExternalReaders, handles[i]);

                container.Dispose();
                container.Reset();
            }

            JobHandle.CombineDependencies(handles).Complete();
            handles.Dispose();

            this.streamBus.Unsubscribe(this);
            this.containers.Clear();
            this.types.Clear();
        }

        /// <inheritdoc/>
        protected override JobHandle OnUpdate(JobHandle handle)
        {
            var handles = new NativeArray<JobHandle>(this.containers.Count, Allocator.TempJob);

            for (var i = 0; i < this.containers.Count; i++)
            {
                var container = this.containers[i];

                // Need both handles because might have no writers or readers in this specific systems
                handles[i] = JobHandle.CombineDependencies(handle, container.ConsumerHandle, container.ProducerHandle);

                Profiler.BeginSample("ReleaseStreams");
                handles[i] = this.streamBus.ReleaseStreams(this, container.ExternalReaders, handles[i]);
                Profiler.EndSample();
                Profiler.BeginSample("AddStreams");
                handles[i] = this.streamBus.AddStreams(this, container.Type, container.Streams, handles[i]);
                Profiler.EndSample();

                container.Reset();
            }

            handle = JobHandle.CombineDependencies(handles);
            handles.Dispose();
            return handle;
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
            where T : struct
        {
            return this.GetOrCreateEventContainer(typeof(T));
        }

        private EventContainer GetOrCreateEventContainer(Type type)
        {
            if (!this.types.TryGetValue(type, out var index))
            {
                index = this.types[type] = this.containers.Count;
                this.containers.Add(new EventContainer(type));
            }

            return this.containers[index];
        }


    }
}