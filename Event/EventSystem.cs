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

    /// <summary>
    /// The EventSystem.
    /// </summary>
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach", Justification = "Unity.")]
    public abstract class EventSystem : JobComponentSystem
    {
        // separate to avoid allocations when iterating
        private readonly List<EventContainer> containers = new List<EventContainer>();
        private readonly Dictionary<Type, int> types = new Dictionary<Type, int>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private bool producerSafety;
        private bool consumerSafety;
#endif

        private JobHandle consumerHandle;
        private StreamShare streamShare;

        public NativeStream.Writer CreateEventWriter<T>(int forEachCount)
            where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (this.producerSafety)
            {
                throw new InvalidOperationException(
                    $"CreateEventWriter must always be balanced by a AddJobHandleForProducer call");
            }

            this.producerSafety = true;
#endif
            return this.GetOrCreateEventContainer<T>().CreateEventStream(forEachCount);
        }

        public void AddJobHandleForProducer<T>(JobHandle handle)
            where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!this.producerSafety)
            {
                throw new InvalidOperationException(
                    $"AddJobHandleForProducer must always be balanced by a GetEventWriter call");
            }

            this.producerSafety = false;
#endif

            this.GetOrCreateEventContainer<T>().AddJobHandleForProducer(handle);
        }

        public JobHandle GetEventReaders<T>(JobHandle handle, out IReadOnlyList<ValueTuple<NativeStream.Reader, int>> readers)
            where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (this.consumerSafety)
            {
                throw new InvalidOperationException(
                    $"GetEventReaders must always be balanced by a AddJobHandleForConsumer call");
            }

            this.consumerSafety = true;
#endif
            var container = this.GetOrCreateEventContainer<T>();

            if (!container.ReadMode)
            {
                container.SetReadMode();
            }

            readers = container.GetReaders();

            return JobHandle.CombineDependencies(container.ProducerHandle, handle);
        }

        public void AddJobHandleForConsumer(JobHandle handle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!this.consumerSafety)
            {
                throw new InvalidOperationException(
                    $"AddJobHandleForProducer must always be balanced by a GetEventWriter call");
            }

            this.consumerSafety = false;
#endif

            this.consumerHandle = JobHandle.CombineDependencies(this.consumerHandle, handle);
        }

        internal void AddExternalReaders(Type type, IReadOnlyList<(NativeStream, int)> externalStreams)
        {
            this.GetOrCreateEventContainer(type).AddReaders(externalStreams);
        }

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            this.streamShare = StreamShare.Instance;

            this.streamShare.Subscribe(this);
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            this.streamShare.Unsubscribe(this);

            for (var index = 0; index < this.containers.Count; index++)
            {
                this.containers[index].Dispose();
            }

            this.containers.Clear();
            this.types.Clear();
        }

        /// <inheritdoc/>
        protected override JobHandle OnUpdate(JobHandle handle)
        {
            handle = JobHandle.CombineDependencies(handle, this.consumerHandle);

            var handles = new NativeArray<JobHandle>(this.containers.Count, Allocator.TempJob);
            var index = 0;

            for (var i = 0; i < this.containers.Count; i++)
            {
                var container = this.containers[i];

                handles[index] = this.streamShare.ReleaseStreams(this, container.ExternalReaders, handle);
                handles[index] = this.streamShare.AddStreams(this, container.Type, container.Streams, handles[index]);
                index++;

                container.Clear();
            }

            this.consumerHandle = default;

            handle = JobHandle.CombineDependencies(handles);
            handles.Dispose();
            return handle;
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
            private readonly List<ValueTuple<NativeStream, int>> streams = new List<ValueTuple<NativeStream, int>>();
            private readonly List<ValueTuple<NativeStream, int>> externalReaders = new List<ValueTuple<NativeStream, int>>();

            private readonly List<ValueTuple<NativeStream.Reader, int>> readers = new List<ValueTuple<NativeStream.Reader, int>>();

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

            public Type Type { get; }

            public List<ValueTuple<NativeStream, int>> Streams => this.streams;

            public List<ValueTuple<NativeStream, int>> ExternalReaders => this.externalReaders;

            public NativeStream.Writer CreateEventStream(int forEachCount)
            {
                if (this.ReadMode)
                {
                    throw new InvalidOperationException(
                        $"CreateEventStream can not be called in read mode.");
                }

                var stream = new NativeStream(forEachCount, Allocator.TempJob);
                this.streams.Add(ValueTuple.Create(stream, forEachCount));

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
                    throw new InvalidOperationException(
                        $"AddJobHandleForProducer can not be called in read mode.");
                }

                this.ProducerHandle = JobHandle.CombineDependencies(this.ProducerHandle, handle);
            }

            /// <summary>
            /// Gets the collection of readers.
            /// </summary>
            /// <returns>Returns a tuple where Item1 is the reader, Item2 is the foreachCount.</returns>
            public IReadOnlyList<ValueTuple<NativeStream.Reader, int>> GetReaders()
            {
                if (!this.ReadMode)
                {
                    throw new InvalidOperationException(
                        $"SetReadMode can not be called in write mode.");
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
                    throw new InvalidOperationException(
                        $"SetReadMode can not be called in read mode.");
                }

                this.ReadMode = true;

                for (var index = 0; index < this.streams.Count; index++)
                {
                    var stream = this.streams[index];
                    this.readers.Add(ValueTuple.Create(stream.Item1.AsReader(), stream.Item2));
                }

                for (var index = 0; index < this.externalReaders.Count; index++)
                {
                    var stream = this.externalReaders[index];
                    this.readers.Add(ValueTuple.Create(stream.Item1.AsReader(), stream.Item2));
                }
            }

            public void AddReaders(IReadOnlyList<(NativeStream, int)> externalStreams)
            {
                if (this.ReadMode)
                {
                    throw new InvalidOperationException(
                        $"AddReaders can not be called in read mode.");
                }

                this.externalReaders.AddRange(externalStreams);
            }

            public void Clear()
            {
                this.ReadMode = false;

                this.streams.Clear();
                this.externalReaders.Clear();
                this.readers.Clear();

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