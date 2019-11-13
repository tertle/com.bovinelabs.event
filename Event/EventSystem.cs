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
        private readonly Dictionary<Type, IEventContainer> types = new Dictionary<Type, IEventContainer>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private bool producerSafety;
        private bool consumerSafety;
#endif

        private JobHandle consumerHandle;

        private interface IEventContainer : IDisposable
        {
            JobHandle Handle { get; }

            IReadOnlyList<ValueTuple<NativeStream.Reader, int>> GetReaders();

            void AddJobHandleForProducer(JobHandle handle);

            void ClearStreams(List<NativeStream> copy);
        }

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
            return ((EventContainer<T>)e).CreateEventStream(forEachCount);
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

            readers = container.GetReaders();
            return container.Handle;
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

        private IEventContainer GetOrCreateEventContainer<T>()
            where T : struct
        {
            if (!this.types.TryGetValue(typeof(T), out var eventContainer))
            {
                eventContainer = this.types[typeof(T)] = new EventContainer<T>();
            }

            return eventContainer;
        }

        private class EventContainer<T> : IEventContainer
            where T : struct
        {
            private readonly List<NativeStream> streams = new List<NativeStream>();
            private readonly List<ValueTuple<NativeStream.Reader, int>> readers = new List<ValueTuple<NativeStream.Reader, int>>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private bool readMode;
#endif

            public JobHandle Handle { get; private set; }

            public NativeStream.Writer CreateEventStream(int forEachCount)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (this.readMode)
                {
                    throw new InvalidOperationException(
                        $"CreateEventStream can not be called in read mode.");
                }
#endif

                var stream = new NativeStream(forEachCount, Allocator.TempJob);
                this.streams.Add(stream);
                this.readers.Add(ValueTuple.Create(stream.AsReader(), forEachCount));

                return stream.AsWriter();
            }

            public void AddJobHandleForProducer(JobHandle handle)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (this.readMode)
                {
                    throw new InvalidOperationException(
                        $"AddJobHandleForProducer can not be called in read mode.");
                }
#endif

                this.Handle = JobHandle.CombineDependencies(this.Handle, handle);
            }

            public IReadOnlyList<ValueTuple<NativeStream.Reader, int>> GetReaders()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.readMode = true;
#endif
                return this.readers;
            }

            public void ClearStreams(List<NativeStream> copy)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.readMode = false;
#endif
                copy.AddRange(this.streams);
                this.streams.Clear();
                this.readers.Clear();
                this.Handle = default;
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