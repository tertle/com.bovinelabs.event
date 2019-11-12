namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using UnityEngine;

    /// <summary>
    /// The EventSystemImpl.
    /// </summary>
    internal class EventSystemImpl : IDisposable
    {
        private readonly Dictionary<Type, IEventContainer> types = new Dictionary<Type, IEventContainer>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private readonly HashSet<Type> safety = new HashSet<Type>();
#endif

        internal interface IEventContainer : IDisposable
        {
            JobHandle Handle { get; set; }

            NativeStream CreateStream(ComponentSystemBase system);

            void Dispose(ComponentSystemBase system);
        }

        internal EventWriter<T> GetEventWriter<T>(ComponentSystemBase system)
            where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!this.safety.Add(typeof(T)))
            {
                throw new ArgumentException(
                    $"GetEventWriter must always be balanced by a AddJobHandleForProducer call");
            }
#endif
            var e = this.GetOrCreateEventContainer<T>();

            return new EventWriter<T>
            {
                Stream = e.CreateStream(system).AsWriter(),
            };
        }

        internal IEventContainer GetOrCreateEventContainer<T>()
            where T : struct
        {
            if (!this.types.TryGetValue(typeof(T), out var eventContainer))
            {
                eventContainer = this.types[typeof(T)] = new EventContainer<T>();
            }

            return eventContainer;
        }

        internal void AddJobHandleForProducer<T>(JobHandle handle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!this.safety.Remove(typeof(T)))
            {
                throw new ArgumentException(
                    $"AddJobHandleForProducer must always be balanced by a GetEventWriter call");
            }
#endif

            if (!this.types.TryGetValue(typeof(T), out var eventContainer))
            {
                Debug.LogWarning("Calling AddJobHandleForProducer on a type that a EventWriter has been created for.");
                return;
            }

            eventContainer.Handle = handle;
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
            private readonly Dictionary<ComponentSystemBase, int> systems = new Dictionary<ComponentSystemBase, int>();
            private readonly List<NativeStream> streams = new List<NativeStream>();

            // TODO?
            public JobHandle Handle { get; set; }

            public NativeStream CreateStream(ComponentSystemBase system)
            {
                var index = this.GetIndex(system);

                // Dispose of old one
                var currentStream = this.streams[index];
                if (currentStream.IsCreated)
                {
                    currentStream.Dispose();
                }

                return this.streams[index] = new NativeStream(JobsUtility.MaxJobThreadCount, Allocator.TempJob);
            }

            public void Dispose(ComponentSystemBase system)
            {
                var index = this.GetIndex(system);

                // Dispose of old one
                var currentStream = this.streams[index];
                if (currentStream.IsCreated)
                {
                    currentStream.Dispose();
                }
            }

            public void Dispose()
            {
                foreach (var stream in this.streams)
                {
                    stream.Dispose();
                }
            }

            private int GetIndex(ComponentSystemBase system)
            {
                if (!this.systems.TryGetValue(system, out var index))
                {
                    index = this.systems.Count;

                    this.systems.Add(system, index);
                    this.streams.Add(default);
                }

                return index;
            }
        }
    }
}