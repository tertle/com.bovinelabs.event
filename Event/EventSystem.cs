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
        private const string producerException = "CreateEventWriter must always be balanced by a AddJobHandleForProducer call";
        private const string consumerException = "GetEventReaders must always be balanced by a AddJobHandleForConsumer call";
        private const string readModeRequired = "Can only be called in read mode.";
        private const string writeModeRequired = "Can not be called in read mode.";
        private const string worldNotImplemented = "WorldMode.Custom requires CustomWorld to be implemented.";

        // separate to avoid allocations when iterating
        private readonly List<EventContainer> containers = new List<EventContainer>();
        private readonly Dictionary<Type, int> types = new Dictionary<Type, int>();

        private bool producerSafety;
        private bool consumerSafety;

        private StreamShare streamShare;

        /// <summary>
        /// The world to use that the event system is linked to.
        /// </summary>
        protected enum WorldMode
        {
            /// <summary>
            /// Uses the systems world, this.World
            /// </summary>
            Parent,

            /// <summary>
            /// Uses the active world, World.Active
            /// </summary>
            Active,

            /// <summary>
            /// Uses a custom world, this.CustomWorld
            /// </summary>
            Custom,
        }

        /// <summary>
        /// Gets the . Override to change the sharing state of the
        /// </summary>
        // ReSharper disable once VirtualMemberNeverOverridden.Global
        protected virtual WorldMode Mode => WorldMode.Parent;

        /// <summary>
        /// Gets the world when using <see cref="WorldMode.Custom"/>.
        /// </summary>
        // ReSharper disable once VirtualMemberNeverOverridden.Global
        protected virtual World CustomWorld => throw new NotImplementedException(worldNotImplemented);

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
            if (this.producerSafety)
            {
                throw new InvalidOperationException(producerException);
            }

            this.producerSafety = true;

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
            if (!this.producerSafety)
            {
                throw new InvalidOperationException(producerException);
            }

            this.producerSafety = false;

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
        public JobHandle GetEventReaders<T>(JobHandle handle, out IReadOnlyList<Tuple2<NativeStream.Reader, int>> readers)
            where T : struct
        {
            if (this.consumerSafety)
            {
                throw new InvalidOperationException(consumerException);
            }

            this.consumerSafety = true;

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
            if (!this.consumerSafety)
            {
                throw new InvalidOperationException(consumerException);
            }

            this.consumerSafety = false;

            this.GetOrCreateEventContainer<T>().AddJobHandleForConsumer(handle);
        }

        /// <summary>
        /// Adds readers from other event systems.
        /// </summary>
        /// <param name="type">The type of event.</param>
        /// <param name="externalStreams">Collection of event streams.</param>
        /// <param name="handle">The dependency for the streams.</param>
        internal void AddExternalReaders(Type type, IReadOnlyList<Tuple2<NativeStream, int>> externalStreams, JobHandle handle)
        {
            var container = this.GetOrCreateEventContainer(type);
            container.AddReaders(externalStreams);

            // updates producer handle because this is what consumers depend on
            container.AddJobHandleForProducer(handle);
        }

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            var world = this.GetEventWorld();
            this.streamShare = StreamShare.GetInstance(world);
            this.streamShare.Subscribe(this);
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
                handles[i] = this.streamShare.ReleaseStreams(this, container.ExternalReaders, handles[i]);

                container.Dispose();
                container.Reset();
            }

            JobHandle.CombineDependencies(handles).Complete();
            handles.Dispose();

            this.streamShare.Unsubscribe(this);
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
                handles[i] = this.streamShare.ReleaseStreams(this, container.ExternalReaders, handles[i]);
                Profiler.EndSample();
                Profiler.BeginSample("AddStreams");
                handles[i] = this.streamShare.AddStreams(this, container.Type, container.Streams, handles[i]);
                Profiler.EndSample();

                container.Reset();
            }

            handle = JobHandle.CombineDependencies(handles);
            handles.Dispose();
            return handle;
        }

        private World GetEventWorld()
        {
            World world;
            switch (this.Mode)
            {
                case WorldMode.Parent:
                    world = this.World;
                    break;
                case WorldMode.Active:
                    world = World.Active;
                    break;
                case WorldMode.Custom:
                    world = this.CustomWorld;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return world;
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

        private class EventContainer
        {
            private readonly List<Tuple2<NativeStream, int>> streams = new List<Tuple2<NativeStream, int>>();
            private readonly List<Tuple2<NativeStream, int>> externalReaders = new List<Tuple2<NativeStream, int>>();

            private readonly List<Tuple2<NativeStream.Reader, int>> readers = new List<Tuple2<NativeStream.Reader, int>>();

            public EventContainer(Type type)
            {
                this.Type = type;
            }

            /// <summary>
            /// Gets a value indicating whether the container is in read only mode.
            /// </summary>
            public bool ReadMode { get; private set; }

            /// <summary>
            /// Gets the producer handle.
            /// </summary>
            public JobHandle ProducerHandle { get; private set; }

            /// <summary>
            /// Gets the producer handle.
            /// </summary>
            public JobHandle ConsumerHandle { get; private set; }

            public Type Type { get; }

            public List<Tuple2<NativeStream, int>> Streams => this.streams;

            public List<Tuple2<NativeStream, int>> ExternalReaders => this.externalReaders;

            public NativeStream.Writer CreateEventStream(int foreachCount)
            {
                if (this.ReadMode)
                {
                    throw new InvalidOperationException(writeModeRequired);
                }

                var stream = new NativeStream(foreachCount, Allocator.Persistent);
                this.streams.Add(new Tuple2<NativeStream, int>(stream, foreachCount));

                return stream.AsWriter();
            }

            /// <summary>
            /// Add a new producer job handle. Can only be called in write mode.
            /// </summary>
            /// <param name="handle">The handle.</param>
            public void AddJobHandleForProducer(JobHandle handle)
            {
                if (this.ReadMode)
                {
                    throw new InvalidOperationException(writeModeRequired);
                }

                this.ProducerHandle = JobHandle.CombineDependencies(this.ProducerHandle, handle);
            }

            /// <summary>
            /// Add a new producer job handle. Can only be called in write mode.
            /// </summary>
            /// <param name="handle">The handle.</param>
            public void AddJobHandleForConsumer(JobHandle handle)
            {
                if (!this.ReadMode)
                {
                    throw new InvalidOperationException(readModeRequired);
                }

                this.ConsumerHandle = JobHandle.CombineDependencies(this.ConsumerHandle, handle);
            }

            /// <summary>
            /// Gets the collection of readers.
            /// </summary>
            /// <returns>Returns a tuple where Item1 is the reader, Item2 is the foreachCount.</returns>
            public IReadOnlyList<Tuple2<NativeStream.Reader, int>> GetReaders()
            {
                if (!this.ReadMode)
                {
                    throw new InvalidOperationException(readModeRequired);
                }

                return this.readers;
            }

            /// <summary>
            /// Set the event to read mode.
            /// </summary>
            public void SetReadMode()
            {
                if (this.ReadMode)
                {
                    throw new InvalidOperationException(writeModeRequired);
                }

                this.ReadMode = true;

                for (var index = 0; index < this.streams.Count; index++)
                {
                    var stream = this.streams[index];
                    this.readers.Add(new Tuple2<NativeStream.Reader, int>(stream.Item1.AsReader(), stream.Item2));
                }

                for (var index = 0; index < this.externalReaders.Count; index++)
                {
                    var stream = this.externalReaders[index];
                    this.readers.Add(new Tuple2<NativeStream.Reader, int>(stream.Item1.AsReader(), stream.Item2));
                }
            }

            public void AddReaders(IEnumerable<Tuple2<NativeStream, int>> externalStreams)
            {
                if (this.ReadMode)
                {
                    throw new InvalidOperationException(writeModeRequired);
                }

                this.externalReaders.AddRange(externalStreams);
            }

            public void Reset()
            {
                this.ReadMode = false;

                this.streams.Clear();
                this.externalReaders.Clear();
                this.readers.Clear();

                this.ConsumerHandle = default;
                this.ProducerHandle = default;
            }

            public void Dispose()
            {
                for (var index = 0; index < this.streams.Count; index++)
                {
                    this.streams[index].Item1.Dispose();
                }
            }
        }
    }
}