// <copyright file="EventContainer.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Jobs;

    /// <summary> The container that holds the actual events of each type. </summary>
    internal sealed class EventContainer : IDisposable
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private const string ProducerException = "CreateEventWriter must always be balanced by a AddJobHandleForProducer call";
        private const string ConsumerException = "GetEventReaders must always be balanced by a AddJobHandleForConsumer call";
        private const string ReadModeRequired = "Can only be called in read mode.";
        private const string WriteModeRequired = "Can not be called in read mode.";
#endif

        private readonly List<Tuple<NativeStream, int>> externalReaders = new List<Tuple<NativeStream, int>>();
        private readonly List<Tuple<NativeStream.Reader, int>> readers = new List<Tuple<NativeStream.Reader, int>>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private bool producerSafety;
        private bool consumerSafety;
#endif

        /// <summary> Initializes a new instance of the <see cref="EventContainer"/> class. </summary>
        /// <param name="type">The event type of this container.</param>
        public EventContainer(Type type)
        {
            this.Type = type;
        }

        /// <summary> Gets a value indicating whether the container is in read only mode. </summary>
        public bool ReadMode { get; private set; }

        /// <summary> Gets the producer handle. </summary>
        public JobHandle ProducerHandle { get; private set; }

        /// <summary> Gets the producer handle. </summary>
        public JobHandle ConsumerHandle { get; private set; }

        /// <summary> Gets the type of event this container holds. </summary>
        public Type Type { get; }

        /// <summary> Gets the list of streams. </summary>
        public List<Tuple<NativeStream, int>> Streams { get; } = new List<Tuple<NativeStream, int>>();

        /// <summary> Gets the list of external readers. </summary>
        public List<Tuple<NativeStream, int>> ExternalReaders => this.externalReaders;

        /// <summary> Create a new stream for the events. </summary>
        /// <param name="foreachCount"> The foreachCount of the <see cref="NativeStream"/>.</param>
        /// <returns> The <see cref="NativeStream.Writer"/>. </returns>
        /// <exception cref="InvalidOperationException"> Throw if previous call not closed or if in read mode. </exception>
        public NativeStream.Writer CreateEventStream(int foreachCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (this.producerSafety)
            {
                throw new InvalidOperationException(ProducerException);
            }

            this.producerSafety = true;

            if (this.ReadMode)
            {
                throw new InvalidOperationException(WriteModeRequired);
            }
#endif

            var stream = new NativeStream(foreachCount, Allocator.Persistent);
            this.Streams.Add(new Tuple<NativeStream, int>(stream, foreachCount));

            return stream.AsWriter();
        }

        /// <summary> Add a new producer job handle. Can only be called in write mode. </summary>
        /// <param name="handle">The handle.</param>
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
        /// <param name="handle">The handle.</param>
        public void AddJobHandleForProducerUnsafe(JobHandle handle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (this.ReadMode)
            {
                throw new InvalidOperationException(WriteModeRequired);
            }
#endif

            this.ProducerHandle = JobHandle.CombineDependencies(this.ProducerHandle, handle);
        }

        /// <summary> Add a new producer job handle. Can only be called in write mode. </summary>
        /// <param name="handle">The handle.</param>
        public void AddJobHandleForConsumer(JobHandle handle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!this.consumerSafety)
            {
                throw new InvalidOperationException(ConsumerException);
            }

            this.consumerSafety = false;

            if (!this.ReadMode)
            {
                throw new InvalidOperationException(ReadModeRequired);
            }
#endif

            this.ConsumerHandle = JobHandle.CombineDependencies(this.ConsumerHandle, handle);
        }

        /// <summary> Gets the collection of readers. </summary>
        /// <returns> Returns a tuple where Item1 is the reader, Item2 is the foreachCount. </returns>
        public IReadOnlyList<Tuple<NativeStream.Reader, int>> GetReaders()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (this.consumerSafety)
            {
                throw new InvalidOperationException(ConsumerException);
            }

            this.consumerSafety = true;

            if (!this.ReadMode)
            {
                throw new InvalidOperationException(ReadModeRequired);
            }
#endif

            return this.readers;
        }

        /// <summary> Check if any readers exist. Requires read mode. </summary>
        /// <returns> True if there is at least 1 reader. </returns>
        /// <exception cref="InvalidOperationException"> Throws if is not in read mode or consumer safety is set. </exception>
        public bool HasReaders()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (this.consumerSafety)
            {
                throw new InvalidOperationException(ConsumerException);
            }

            if (!this.ReadMode)
            {
                throw new InvalidOperationException(ReadModeRequired);
            }
#endif

            return this.readers.Count != 0;
        }

        /// <summary> Set the event to read mode. </summary>
        public void SetReadMode()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (this.ReadMode)
            {
                throw new InvalidOperationException(WriteModeRequired);
            }
#endif
            this.ReadMode = true;

            foreach (var stream in this.Streams)
            {
                this.readers.Add(new Tuple<NativeStream.Reader, int>(stream.Item1.AsReader(), stream.Item2));
            }

            foreach (var stream in this.externalReaders)
            {
                this.readers.Add(new Tuple<NativeStream.Reader, int>(stream.Item1.AsReader(), stream.Item2));
            }
        }

        /// <summary> Add readers to the container. Requires read mode.  </summary>
        /// <param name="externalStreams"> The readers to be added. </param>
        /// <exception cref="InvalidOperationException"> Throw if not in read mode. </exception>
        public void AddReaders(IEnumerable<Tuple<NativeStream, int>> externalStreams)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (this.ReadMode)
            {
                throw new InvalidOperationException(WriteModeRequired);
            }
#endif

            this.externalReaders.AddRange(externalStreams);
        }

        /// <summary> Reset and clears the container ready for next frame. </summary>
        public void Reset()
        {
            this.ReadMode = false;

            this.Streams.Clear();
            this.externalReaders.Clear();
            this.readers.Clear();

            this.ConsumerHandle = default;
            this.ProducerHandle = default;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (var t in this.Streams)
            {
                t.Item1.Dispose();
            }

            this.Reset();
        }
    }
}