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
        private readonly HashSet<Type> safety = new HashSet<Type>();
#endif

        private interface IEventContainer : IDisposable
        {
            JobHandle Handle { get; }

            NativeStream Stream { get; set; }

            void AddJobHandleForProducer(JobHandle handle);

            JobHandle GetReader(JobHandle inputHandle, out NativeStream.Reader reader);

            JobHandle OnUpdate(JobHandle handle);
        }

        internal NativeQueue<T> CreateEventWriter<T>()
            where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!this.safety.Add(typeof(T)))
            {
                throw new InvalidOperationException(
                    $"CreateEventWriter must always be balanced by a AddJobHandleForProducer call");
            }
#endif
            var e = this.GetOrCreateEventContainer<T>();
            return ((EventContainer<T>)e).CreateEventQueue();
        }

        internal void AddJobHandleForProducer<T>(JobHandle handle)
            where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!this.safety.Remove(typeof(T)))
            {
                throw new InvalidOperationException(
                    $"AddJobHandleForProducer must always be balanced by a GetEventWriter call");
            }
#endif

            this.GetOrCreateEventContainer<T>().AddJobHandleForProducer(handle);
        }

        internal JobHandle GetEventReader<T>(JobHandle handle, out NativeStream.Reader reader)
            where T : struct
        {
            return this.GetOrCreateEventContainer<T>().GetReader(handle, out reader);
        }

        internal JobHandle OnUpdate(JobHandle handle)
        {
            var handles = new NativeArray<JobHandle>(this.types.Count, Allocator.TempJob);

            var index = 0;

            foreach (var e in this.types)
            {
                var eventHandle = JobHandle.CombineDependencies(handle, e.Value.Handle);

                // TODO PRESENTATION
                if (e.Value.Stream.IsCreated)
                {
                    eventHandle = e.Value.Stream.Dispose(eventHandle);
                    e.Value.Stream = default;
                }

                handles[index++] = e.Value.OnUpdate(eventHandle);
            }

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
            private readonly List<NativeQueue<T>> queues = new List<NativeQueue<T>>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private bool readMode;
#endif

            public JobHandle Handle { get; private set; }

            public NativeStream Stream { get; set; }

            public NativeQueue<T> CreateEventQueue()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (this.readMode)
                {
                    throw new InvalidOperationException(
                        $"CreateEventQueue can not be called in read mode.");
                }
#endif

                var queue = new NativeQueue<T>(Allocator.TempJob);
                this.queues.Add(queue);
                return queue;
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

            public JobHandle GetReader(JobHandle inputHandle, out NativeStream.Reader reader)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.readMode = true;
#endif

                this.Handle = JobHandle.CombineDependencies(this.Handle, inputHandle);

                // if stream already created, it's already been requested this frame just pass it back.
                if (!this.Stream.IsCreated)
                {
                    if (this.queues.Count != 0)
                    {
                        this.Stream = new NativeStream(this.queues.Count, Allocator.Persistent);

                        var handles = new NativeArray<JobHandle>(this.queues.Count, Allocator.TempJob);

                        for (var i = 0; i < this.queues.Count; i++)
                        {
                            handles[i] = new ConvertQueueToStreamJob<T>
                                {
                                    Queue = this.queues[i],
                                    StreamWriter = this.Stream.AsWriter(),
                                    ForEachIndex = i,
                                }
                                .Schedule(this.Handle);

                            // Dispose the queues
                            handles[i] = this.queues[i].Dispose(handles[i]);
                        }

                        this.queues.Clear();

                        this.Handle = JobHandle.CombineDependencies(handles);
                        handles.Dispose();
                    }
                }

                Assert.AreEqual(0, this.queues.Count);

                reader = this.Stream.AsReader();
                return this.Handle;
            }

            public JobHandle OnUpdate(JobHandle handle)
            {
                this.Handle = default;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.readMode = false;
#endif

                if (this.Stream.IsCreated)
                {
                    throw new InvalidOperationException("System must be reset before calling OnUpdate.");
                }

                if (this.queues.Count != 0)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    Debug.LogWarning($"Unhandled events of type {typeof(T)}");
#endif

                    var handles = new NativeArray<JobHandle>(this.queues.Count, Allocator.TempJob);

                    for (var index = 0; index < this.queues.Count; index++)
                    {
                        handles[index] = this.queues[index].Dispose(handle);
                    }

                    handle = JobHandle.CombineDependencies(handles);
                    this.queues.Clear();
                    handles.Dispose();
                }

                return handle;
            }

            public void Dispose()
            {
                foreach (var stream in this.queues)
                {
                    stream.Dispose();
                }
            }
        }
    }

    [BurstCompile] // doesn't work
    public struct ConvertQueueToStreamJob<T> : IJob
        where T : struct
    {
        public NativeQueue<T> Queue;

        [NativeDisableContainerSafetyRestriction]
        public NativeStream.Writer StreamWriter;

        public int ForEachIndex;

        public void Execute()
        {
            this.StreamWriter.BeginForEachIndex(this.ForEachIndex);

            while (this.Queue.TryDequeue(out var item))
            {
                this.StreamWriter.Write(item);
            }

            this.StreamWriter.EndForEachIndex();
        }
    }
}