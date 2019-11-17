// <copyright file="StreamShare.cs" company="BovineLabs">
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
    using UnityEngine;
    using UnityEngine.Assertions;

    /// <summary>
    /// The stream sharing backend.
    /// </summary>
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach", Justification = "Unity")]
    internal class StreamShare
    {
        private static readonly Dictionary<World, StreamShare> Instances = new Dictionary<World, StreamShare>();

        private readonly ObjectPool<HashSet<EventSystem>> pool = new ObjectPool<HashSet<EventSystem>>(() => new HashSet<EventSystem>());

        private readonly List<EventSystem> subscribers = new List<EventSystem>();
        private readonly Dictionary<NativeStream, HashSet<EventSystem>> streams = new Dictionary<NativeStream, HashSet<EventSystem>>();

        private StreamShare()
        {
        }

        /// <summary>
        /// Get an instance of StreamShare linked to a world.
        /// </summary>
        /// <param name="world">The world.</param>
        /// <returns>A shared instance of StreamShare.</returns>
        internal static StreamShare GetInstance(World world)
        {
            if (!Instances.TryGetValue(world, out var streamShare))
            {
                streamShare = Instances[world] = new StreamShare();
            }

            return streamShare;
        }

        /// <summary>
        /// Subscribe an EventSystem to get reader updates.
        /// </summary>
        /// <param name="eventSystem">The event system.</param>
        internal void Subscribe(EventSystem eventSystem)
        {
            Assert.IsFalse(this.subscribers.Contains(eventSystem));
            this.subscribers.Add(eventSystem);
        }

        /// <summary>
        /// Unsubscribe an EventSystem to stop getting reader updates.
        /// </summary>
        /// <param name="eventSystem">The event system.</param>
        internal void Unsubscribe(EventSystem eventSystem)
        {
            Assert.IsTrue(this.subscribers.Contains(eventSystem));
            this.subscribers.Remove(eventSystem);

            // Must release streams before call unsubscribe.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            foreach (var streamPair in this.streams)
            {
                Assert.IsFalse(streamPair.Value.Remove(eventSystem));
            }
#endif
        }

        /// <summary>
        /// Add a set of event streams to be shared with other systems.
        /// </summary>
        /// <param name="owner">The system that owns the streams.</param>
        /// <param name="type">The type of the event.</param>
        /// <param name="newStreams">The streams.</param>
        /// <param name="consumerHandle">The dependency handle for these streams.</param>
        /// <returns>The new dependency handle.</returns>
        /// <exception cref="ArgumentException">Thrown  if this owner is not subscribed.</exception>
        internal JobHandle AddStreams(EventSystem owner, Type type, IReadOnlyList<(NativeStream, int)> newStreams, JobHandle consumerHandle)
        {
            if (!this.subscribers.Contains(owner))
            {
                throw new ArgumentException("Owner not subscribed");
            }

            if (newStreams.Count == 0)
            {
                return consumerHandle;
            }

            if (this.subscribers.Count == 1)
            {
                // No subscribers other than ourselves, just dispose the streams
                JobHandle handle = consumerHandle;

                for (var index = 0; index < newStreams.Count; index++)
                {
                    var stream = newStreams[index];
                    handle = JobHandle.CombineDependencies(handle, stream.Item1.Dispose(consumerHandle));
                }

                return handle;
            }

            for (var index = 0; index < newStreams.Count; index++)
            {
                var stream = newStreams[index];
                var systems = this.pool.Get();

                this.streams.Add(stream.Item1, systems);

                for (var i = 0; i < this.subscribers.Count; i++)
                {
                    var subscriber = this.subscribers[i];
                    if (subscriber == owner)
                    {
                        continue;
                    }

                    systems.Add(subscriber);
                }
            }

            // Fire off these new readers
            for (var i = 0; i < this.subscribers.Count; i++)
            {
                var subscriber = this.subscribers[i];
                if (subscriber == owner)
                {
                    continue;
                }

                subscriber.AddExternalReaders(type, newStreams, consumerHandle);
            }

            return consumerHandle;
        }

        /// <summary>
        /// Return a set of streams that the system has finished reading.
        /// </summary>
        /// <param name="owner">The system the streams are coming from.</param>
        /// <param name="streamsToRelease">The collection of streams to be released.</param>
        /// <param name="inputHandle">The dependency handle.</param>
        /// <returns>New dependency handle.</returns>
        internal JobHandle ReleaseStreams(EventSystem owner, IReadOnlyList<(NativeStream, int)> streamsToRelease, JobHandle inputHandle)
        {
            var handle = inputHandle;

            for (var index = 0; index < streamsToRelease.Count; index++)
            {
                var stream = streamsToRelease[index].Item1;

                Assert.IsTrue(this.streams.ContainsKey(stream));

                var events = this.streams[stream];

                // Remove the owner handle
                bool result = events.Remove(owner);
                Assert.IsTrue(result);

                // No one else using stream, need to dispose
                if (events.Count == 0)
                {
                    this.pool.Return(events);
                    this.streams.Remove(stream);

                    var newHandle = stream.Dispose(inputHandle);
                    handle = JobHandle.CombineDependencies(handle, newHandle);
                }
            }

            return handle;
        }

        [RuntimeInitializeOnLoadMethod]
        private static void Reset()
        {
            Instances.Clear();
        }
    }
}