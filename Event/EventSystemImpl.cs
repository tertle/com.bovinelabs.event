namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine;
    using UnityEngine.Assertions;

    /// <summary>
    /// The EventSystemImpl.
    /// </summary>
    internal class EventSystemImpl : IDisposable
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

            IReadOnlyList<ValueTuple<NativeStream.Reader, int>> Readers { get; }

            void AddJobHandleForProducer(JobHandle handle);

            void ClearStreams(List<NativeStream> copy);
        }

        internal NativeStream.Writer CreateEventWriter<T>(int forEachCount)
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

        internal void AddJobHandleForProducer<T>(JobHandle handle)
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

        internal JobHandle GetEventReaders<T>(out IReadOnlyList<ValueTuple<NativeStream.Reader, int>> readers)
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

            readers = container.Readers;
            return container.Handle;
        }

        internal void AddJobHandleForConsumer(JobHandle handle)
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

        internal JobHandle OnUpdate(JobHandle handle)
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

        public void Dispose()
        {
            foreach (var t in this.types)
            {
                t.Value.Dispose();
            }
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

            public IReadOnlyList<ValueTuple<NativeStream.Reader, int>> Readers => this.readers;

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