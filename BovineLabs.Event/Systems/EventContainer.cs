// <copyright file="EventContainer.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using System;
    using System.Collections.Generic;
    using BovineLabs.Event.Containers;
    using Unity.Collections;
    using Unity.Jobs;

    /// <summary> The container that holds the actual events of each type. </summary>
    internal sealed class EventContainer : IDisposable
    {
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        private const string ProducerException =
            "CreateEventWriter must always be balanced by a AddJobHandleForProducer call";

        private const string ConsumerException =
            "GetEventReaders must always be balanced by a AddJobHandleForConsumer call";

        private const string ReadModeRequired = "Can only be called in read mode.";
#endif

        private readonly bool usePersistentAllocator;

        private readonly List<NativeEventStream> externalReaders =
            new List<NativeEventStream>();

        private readonly List<NativeEventStream> deferredExternalReaders =
            new List<NativeEventStream>();

        private readonly List<NativeEventStream.Reader> readers =
            new List<NativeEventStream.Reader>();

        private readonly List<NativeEventStream> deferredStreams =
            new List<NativeEventStream>();

        private bool isReadMode;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private bool producerSafety;
        private bool consumerSafety;
#endif

        /// <summary> Initializes a new instance of the <see cref="EventContainer"/> class. </summary>
        /// <param name="type"> The event type of this container. </param>
        /// <param name="usePersistentAllocator"> Should the container use a persistent container instead of TempJob. </param>
        public EventContainer(Type type, bool usePersistentAllocator)
        {
            this.usePersistentAllocator = usePersistentAllocator;
            this.Type = type;
        }

        /// <summary> Gets the producer handle. </summary>
        public JobHandle ProducerHandle { get; private set; }

        /// <summary> Gets the producer handle. </summary>
        public JobHandle ConsumerHandle { get; private set; }

        /// <summary> Gets the producer handle. </summary>
        public JobHandle DeferredProducerHandle { get; private set; }

        /// <summary> Gets the type of event this container holds. </summary>
        public Type Type { get; }

        /// <summary> Gets the list of streams. </summary>
        public List<NativeEventStream> Streams { get; } = new List<NativeEventStream>();

        /// <summary> Gets the list of external readers. </summary>
        public List<NativeEventStream> ExternalReaders => this.externalReaders;

        /// <summary> Gets the list of external readers. </summary>
        public List<NativeEventStream> DeferredExternalReaders => this.deferredExternalReaders;

        /// <summary> Create a new stream for the events. </summary>
        /// <param name="foreachCount"> The foreach count. A negative value will make it a thread stream. </param>
        /// <returns> The <see cref="NativeEventStream.Writer"/> . </returns>
        /// <exception cref="InvalidOperationException"> Throw if previous call not closed or if in read mode. </exception>
        public NativeEventStream.Writer CreateEventStream(int foreachCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (this.producerSafety)
            {
                throw new InvalidOperationException(ProducerException);
            }

            this.producerSafety = true;
#endif

            var allocator = this.usePersistentAllocator ? Allocator.Persistent : Allocator.TempJob;

            var stream = foreachCount < 0 ? new NativeEventStream(allocator) : new NativeEventStream(foreachCount, allocator);

            if (this.isReadMode)
            {
                this.deferredStreams.Add(stream);
            }
            else
            {
                this.Streams.Add(stream);
            }

            return stream.AsWriter();
        }

        /// <summary> Add a new producer job handle. Can only be called in write mode. </summary>
        /// <param name="handle"> The handle. </param>
        public void AddJobHandleForProducer(JobHandle handle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!this.producerSafety)
            {
                throw new InvalidOperationException(ProducerException);
            }

            this.producerSafety = false;
#endif

            this.AddJobHandleForProducerUnsafe(handle);
        }

        /// <summary> Add a new producer job handle while skipping the producer safety check. Can only be called in write mode. </summary>
        /// <param name="handle"> The handle. </param>
        public void AddJobHandleForProducerUnsafe(JobHandle handle)
        {
            if (this.isReadMode)
            {
                this.DeferredProducerHandle = JobHandle.CombineDependencies(this.DeferredProducerHandle, handle);
            }
            else
            {
                this.ProducerHandle = JobHandle.CombineDependencies(this.ProducerHandle, handle);
            }
        }

        /// <summary> Add a new producer job handle. Can only be called in write mode. </summary>
        /// <param name="handle"> The handle. </param>
        public void AddJobHandleForConsumer(JobHandle handle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!this.consumerSafety)
            {
                throw new InvalidOperationException(ConsumerException);
            }

            this.consumerSafety = false;

            if (!this.isReadMode)
            {
                throw new InvalidOperationException(ReadModeRequired);
            }
#endif

            this.ConsumerHandle = JobHandle.CombineDependencies(this.ConsumerHandle, handle);
        }

        /// <summary> Gets the collection of readers. </summary>
        /// <returns> Returns the reader. </returns>
        public IReadOnlyList<NativeEventStream.Reader> GetReaders()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (this.consumerSafety)
            {
                throw new InvalidOperationException(ConsumerException);
            }

            this.consumerSafety = true;
#endif

            this.SetReadMode();

            return this.readers;
        }

        /// <summary> Check if any readers exist. Requires read mode. </summary>
        /// <returns> True if there is at least 1 reader. </returns>
        /// <exception cref="InvalidOperationException"> Throws if is not in read mode or consumer safety is set. </exception>
        public int GetReadersCount()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (this.consumerSafety)
            {
                throw new InvalidOperationException(ConsumerException);
            }
#endif

            this.SetReadMode();
            return this.readers.Count;
        }

        /// <summary> Add readers to the container. Requires read mode.  </summary>
        /// <param name="externalStreams"> The readers to be added. </param>
        /// <exception cref="InvalidOperationException"> Throw if not in read mode. </exception>
        public void AddReaders(IEnumerable<NativeEventStream> externalStreams)
        {
            if (this.isReadMode)
            {
                this.deferredExternalReaders.AddRange(externalStreams);
            }
            else
            {
                this.externalReaders.AddRange(externalStreams);
            }
        }

        /// <summary> Update for the next frame. </summary>
        public void Update()
        {
            this.isReadMode = false;

            // Clear our containers
            this.Streams.Clear();
            this.externalReaders.Clear();
            this.readers.Clear();

            // Copy our deferred containers for the next frame
            this.Streams.AddRange(this.deferredStreams);
            this.deferredStreams.Clear();

            this.externalReaders.AddRange(this.deferredExternalReaders);
            this.deferredExternalReaders.Clear();

            // Reset handles
            this.ConsumerHandle = default;
            this.ProducerHandle = this.DeferredProducerHandle;
            this.DeferredProducerHandle = default;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.ProducerHandle.Complete();
            for (var index = 0; index < this.Streams.Count; index++)
            {
                this.Streams[index].Dispose();
            }

            this.DeferredProducerHandle.Complete();
            for (var index = 0; index < this.deferredStreams.Count; index++)
            {
                this.deferredStreams[index].Dispose();
            }
        }

        /// <summary> Set the event to read mode. </summary>
        private void SetReadMode()
        {
            if (this.isReadMode)
            {
                return;
            }

            this.isReadMode = true;

            for (var index = 0; index < this.Streams.Count; index++)
            {
                var stream = this.Streams[index];
                this.readers.Add(stream.AsReader());
            }

            for (var index = 0; index < this.externalReaders.Count; index++)
            {
                var stream = this.externalReaders[index];
                this.readers.Add(stream.AsReader());
            }
        }
    }
}