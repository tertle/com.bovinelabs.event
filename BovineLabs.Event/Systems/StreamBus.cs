// <copyright file="StreamBus.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using BovineLabs.Event.Data;
    using BovineLabs.Event.Utility;
    using Unity.Collections;
    using Unity.Jobs;
    using UnityEngine;
    using UnityEngine.Assertions;

    /// <summary>
    /// The stream sharing backend.
    /// </summary>
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach", Justification = "Unity")]
    internal class StreamBus
    {
        private static readonly Dictionary<string, StreamBus> Instances = new Dictionary<string, StreamBus>();

        private readonly List<EventSystem> subscribers = new List<EventSystem>();
        private readonly Dictionary<NativeStream, StreamHandles> streams = new Dictionary<NativeStream, StreamHandles>(new NativeStreamCompare());
        private readonly ObjectPool<StreamHandles> pool = new ObjectPool<StreamHandles>(() => new StreamHandles());

        private StreamBus()
        {
        }

        /// <summary>
        /// Get an instance of StreamBus linked to a key.
        /// </summary>
        /// <param name="key">The bus key.</param>
        /// <returns>A shared instance of StreamBus.</returns>
        internal static StreamBus GetInstance(string key)
        {
            if (!Instances.TryGetValue(key, out var streamShare))
            {
                streamShare = Instances[key] = new StreamBus();
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
                Assert.IsFalse(streamPair.Value.Systems.Remove(eventSystem));
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
        internal JobHandle AddStreams(EventSystem owner, Type type, IReadOnlyList<ValueTuple<NativeStream, int>> newStreams, JobHandle consumerHandle)
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
                    var stream = newStreams[index].Item1;
                    handle = JobHandle.CombineDependencies(handle, stream.Dispose(consumerHandle));
                }

                return handle;
            }

            for (var index = 0; index < newStreams.Count; index++)
            {
                var stream = newStreams[index];
                var handles = this.pool.Get();
                handles.Handle = consumerHandle;

                this.streams.Add(stream.Item1, handles);

                for (var i = 0; i < this.subscribers.Count; i++)
                {
                    var subscriber = this.subscribers[i];
                    if (subscriber == owner)
                    {
                        continue;
                    }

                    handles.Systems.Add(subscriber);
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
        internal JobHandle ReleaseStreams(EventSystem owner, IReadOnlyList<ValueTuple<NativeStream, int>> streamsToRelease, JobHandle inputHandle)
        {
            JobHandle outputHandle = inputHandle;

            for (var index = 0; index < streamsToRelease.Count; index++)
            {
                var stream = streamsToRelease[index].Item1;

                Assert.IsTrue(this.streams.ContainsKey(stream));

                var handles = this.streams[stream];

                // Remove the owner handle
                bool result = handles.Systems.Remove(owner);
                Assert.IsTrue(result);

                var handle = JobHandle.CombineDependencies(handles.Handle, inputHandle);

                // No one else using stream, need to dispose
                if (handles.Systems.Count == 0)
                {
                    this.pool.Return(handles);
                    this.streams.Remove(stream);

                    handle = stream.Dispose(handle);
                }
                else
                {
                    handles.Handle = handle;
                }

                outputHandle = JobHandle.CombineDependencies(outputHandle, handle);
            }

            return outputHandle;
        }

        [RuntimeInitializeOnLoadMethod]
        private static void Reset()
        {
            Instances.Clear();
        }

        private class StreamHandles
        {
            public HashSet<EventSystem> Systems { get; } = new HashSet<EventSystem>();

            public JobHandle Handle { get; set; }
        }

        /// <summary>
        /// Allocation free NativeStream comparer.
        /// </summary>
        private class NativeStreamCompare : IEqualityComparer<NativeStream>
        {
            public bool Equals(NativeStream x, NativeStream y)
            {
                var xi = (NativeStreamImposter)x;
                var yi = (NativeStreamImposter)y;

                return xi.Equals(yi);
            }

            public int GetHashCode(NativeStream stream)
            {
                var imposter = (NativeStreamImposter)stream;
                return imposter.GetHashCode();
            }
        }
    }
}