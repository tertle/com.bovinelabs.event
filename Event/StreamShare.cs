namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Unity.Collections;
    using Unity.Jobs;
    using UnityEngine.Assertions;

    /// <summary>
    /// The StreamShare.
    /// </summary>
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach", Justification = "Unity")]
    internal class StreamShare
    {
        private readonly ObjectPool<HashSet<EventSystem>> pool = new ObjectPool<HashSet<EventSystem>>(() => new HashSet<EventSystem>());

        private readonly List<EventSystem> subscribers = new List<EventSystem>();
        private readonly Dictionary<NativeStream, HashSet<EventSystem>> streams = new Dictionary<NativeStream, HashSet<EventSystem>>();

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

        public JobHandle AddStreams(EventSystem owner, Type type, IReadOnlyList<(NativeStream, int)> newStreams, JobHandle inputHandle)
        {
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
                JobHandle handle = inputHandle;

                for (var index = 0; index < newStreams.Count; index++)
                {
                    var stream = newStreams[index];
                    handle = JobHandle.CombineDependencies(handle, stream.Item1.Dispose(inputHandle));
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
                    subscriber.AddExternalReaders(type, newStreams);
                }
            }

            return inputHandle;
        }

        public JobHandle ReleaseStreams(EventSystem owner, IReadOnlyList<(NativeStream, int)> newStreams, JobHandle inputHandle)
        {
            var handle = inputHandle;

            for (var index = 0; index < newStreams.Count; index++)
            {
                var stream = newStreams[index];
                var newHandle = this.RemoveOwner(owner, stream.Item1, inputHandle);

                handle = JobHandle.CombineDependencies(handle, newHandle);
            }

            return handle;
        }

        private JobHandle RemoveOwner(EventSystem owner, NativeStream stream, JobHandle handle)
        {
            Assert.IsTrue(this.streams.ContainsKey(stream));

            var set = this.streams[stream];

            bool result = set.Remove(owner);
            Assert.IsTrue(result);

            // No one else using stream, need to dispose
            if (set.Count == 0)
            {
                this.pool.Return(set);
                this.streams.Remove(stream);

                handle = JobHandle.CombineDependencies(handle, stream.Dispose(handle));
            }

            return handle;
        }
    }
}