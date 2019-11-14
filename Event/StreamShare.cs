namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using Unity.Collections;
    using Unity.Jobs;
    using UnityEngine;
    using UnityEngine.Assertions;

    /// <summary>
    /// The StreamShare.
    /// </summary>
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach", Justification = "Unity")]
    internal class StreamShare
    {
        private static StreamShare instance;

        internal static StreamShare Instance => instance ?? (instance = new StreamShare());

        private readonly ObjectPool<HashSet<EventSystem>> pool = new ObjectPool<HashSet<EventSystem>>(() => new HashSet<EventSystem>());

        private readonly List<EventSystem> subscribers = new List<EventSystem>();
        private readonly Dictionary<NativeStream, HashSet<EventSystem>> streams = new Dictionary<NativeStream, HashSet<EventSystem>>();

        private StreamShare()
        {
        }

        [RuntimeInitializeOnLoadMethod]
        private static void Reset()
        {
            instance = null;
        }

        public void Subscribe(EventSystem eventSystem)
        {
            Assert.IsFalse(this.subscribers.Contains(eventSystem));
            this.subscribers.Add(eventSystem);
        }

        public void Unsubscribe(EventSystem eventSystem)
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

        public JobHandle AddStreams(EventSystem owner, Type type, IReadOnlyList<(NativeStream, int)> newStreams, JobHandle consumerHandle)
        {
            if (newStreams.Count == 0)
            {
                return consumerHandle;
            }

            if (this.subscribers.Count == 0)
            {
                throw new InvalidOperationException("No subscribers");
            }

            if (this.subscribers.Count == 1)
            {
                if (!this.subscribers.Contains(owner))
                {
                    throw new ArgumentException("Owner not subscribed");
                }

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

        public JobHandle ReleaseStreams(EventSystem owner, IReadOnlyList<(NativeStream, int)> newStreams, JobHandle inputHandle)
        {
            var handle = inputHandle;

            for (var index = 0; index < newStreams.Count; index++)
            {
                var stream = newStreams[index].Item1;

                Assert.IsTrue(this.streams.ContainsKey(stream));

                var set = this.streams[stream];

                bool result = set.Remove(owner);
                Assert.IsTrue(result);

                // No one else using stream, need to dispose
                if (set.Count == 0)
                {
                    this.pool.Return(set);
                    this.streams.Remove(stream);

                    var newHandle = stream.Dispose(inputHandle);
                    handle = JobHandle.CombineDependencies(handle, newHandle);
                }
            }

            return handle;
        }
    }
}