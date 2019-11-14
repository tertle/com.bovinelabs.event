// <copyright file="EventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    /// The EventSystem.
    /// </summary>
    public abstract class EventSystem : JobComponentSystem
    {
        private readonly Dictionary<Type, EventContainer> types = new Dictionary<Type, EventContainer>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private bool producerSafety;
        private bool consumerSafety;
#endif

        private JobHandle consumerHandle;

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
            var e = this.GetOrCreateEventContainer<T>();
            return ((EventContainer)e).CreateEventStream(forEachCount);
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
                // TODO
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

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            foreach (var t in this.types)
            {
                t.Value.Dispose();
            }
        }

        protected override JobHandle OnUpdate(JobHandle handle)
        {
            var handles = new NativeArray<JobHandle>(this.types.Count, Allocator.TempJob);

            handle = JobHandle.CombineDependencies(handle, this.consumerHandle);

            var index = 0;

            foreach (var e in this.types)
            {
                var streams = new List<NativeStream>();

                e.Value.ClearStreams(streams);

                var eventHandle = handle;

                // TODO COPY
                foreach (var s in streams)
                {
                    eventHandle = s.Dispose(eventHandle);
                }

                handles[index++] = eventHandle;
            }

            this.consumerHandle = default;

            handle = JobHandle.CombineDependencies(handles);
            handles.Dispose();
            return handle;
        }

        private EventContainer GetOrCreateEventContainer<T>()
            where T : struct
        {
            if (!this.types.TryGetValue(typeof(T), out var eventContainer))
            {
                eventContainer = this.types[typeof(T)] = new EventContainer();
            }

            return eventContainer;
        }

        private class EventContainer
        {
            private readonly List<NativeStream> streams = new List<NativeStream>();
            private readonly List<ValueTuple<NativeStream.Reader, int>> readers = new List<ValueTuple<NativeStream.Reader, int>>();

            /// <summary>
            /// Gets a value indicating whether the container is in read only mode.
            /// </summary>
            public bool ReadMode { get; private set; }

            /// <summary>
            /// Gets the producer handle.
            /// </summary>
            public JobHandle ProducerHandle { get; private set; }

            public NativeStream.Writer CreateEventStream(int forEachCount)
            {
                if (this.ReadMode)
                {
                    throw new InvalidOperationException(
                        $"CreateEventStream can not be called in read mode.");
                }

                var stream = new NativeStream(forEachCount, Allocator.TempJob);
                this.streams.Add(stream);
                this.readers.Add(ValueTuple.Create(stream.AsReader(), forEachCount));

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
            /// <param name="externalReadStreams">An optional collection of external streams to add.</param>
            public void SetReadMode(IReadOnlyList<ValueTuple<NativeStream.Reader, int>> externalReadStreams = null)
            {
                if (this.ReadMode)
                {
                    throw new InvalidOperationException(
                        $"SetReadMode can not be called in read mode.");
                }

                this.ReadMode = true;

                if (externalReadStreams != null)
                {
                    this.readers.AddRange(externalReadStreams);
                }
            }

            public void ClearStreams(List<NativeStream> copy)
            {
                this.ReadMode = false;
                copy.AddRange(this.streams);
                this.streams.Clear();
                this.readers.Clear(); // TODO?
                this.ProducerHandle = default;
            }

            public void Dispose()
            {
                foreach (var stream in this.streams)
                {
                    stream.Dispose();
                }
            }
        }
    }
}