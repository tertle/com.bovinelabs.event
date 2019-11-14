namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Jobs;
    using UnityEngine.Assertions;

    /// <summary>
    /// The StreamShare.
    /// </summary>
    internal class StreamShare
    {
        /*private readonly Dictionary<EventSystem, Dictionary<Type, StreamContainer>> subscribers =
            new Dictionary<EventSystem, Dictionary<Type, StreamContainer>>();*/

        private readonly ObjectPool<HashSet<EventSystem>> pool = new ObjectPool<HashSet<EventSystem>>(() => new HashSet<EventSystem>());

        private readonly HashSet<EventSystem> subscribers = new HashSet<EventSystem>();
        private readonly Dictionary<NativeStream, HashSet<EventSystem>> streams = new Dictionary<NativeStream, HashSet<EventSystem>>();

        public void Subscribe(EventSystem eventSystem)
        {
            this.subscribers.Add(eventSystem);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="eventSystem"></param>
        public void Unsubscribe(EventSystem eventSystem)
        {
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

                foreach (var (stream, _) in newStreams)
                {
                    handle = JobHandle.CombineDependencies(handle, stream.Dispose(inputHandle));
                }

                return handle;
            }

            foreach (var (stream, _) in newStreams)
            {
                var systems = this.pool.Get();

                this.streams.Add(stream, systems);

                foreach (var subscriber in this.subscribers)
                {
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

            foreach (var (stream, _) in newStreams)
            {
                var newHandle = this.RemoveOwner(owner, stream, inputHandle);

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