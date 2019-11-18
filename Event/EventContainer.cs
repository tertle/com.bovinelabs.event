// <copyright file="EventContainer.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Jobs;

    public class EventContainer : IDisposable
    {
        private const string ProducerException = "CreateEventWriter must always be balanced by a AddJobHandleForProducer call";
        private const string ConsumerException = "GetEventReaders must always be balanced by a AddJobHandleForConsumer call";
        private const string ReadModeRequired = "Can only be called in read mode.";
        private const string WriteModeRequired = "Can not be called in read mode.";

        private readonly List<Tuple<NativeStream, int>> streams = new List<Tuple<NativeStream, int>>();
        private readonly List<Tuple<NativeStream, int>> externalReaders = new List<Tuple<NativeStream, int>>();

        private readonly List<Tuple<NativeStream.Reader, int>> readers = new List<Tuple<NativeStream.Reader, int>>();

        private bool producerSafety;
        private bool consumerSafety;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventContainer"/> class.
        /// </summary>
        /// <param name="type">The event type of this container.</param>
        public EventContainer(Type type)
        {
            this.Type = type;
        }

        /// <summary>
        /// Gets a value indicating whether the container is in read only mode.
        /// </summary>
        public bool ReadMode { get; private set; }

        /// <summary>
        /// Gets the producer handle.
        /// </summary>
        public JobHandle ProducerHandle { get; private set; }

        /// <summary>
        /// Gets the producer handle.
        /// </summary>
        public JobHandle ConsumerHandle { get; private set; }

        public Type Type { get; }

        public List<Tuple<NativeStream, int>> Streams => this.streams;

        public List<Tuple<NativeStream, int>> ExternalReaders => this.externalReaders;

        public NativeStream.Writer CreateEventStream(int foreachCount)
        {
            if (this.producerSafety)
            {
                throw new InvalidOperationException(ProducerException);
            }

            this.producerSafety = true;

            if (this.ReadMode)
            {
                throw new InvalidOperationException(WriteModeRequired);
            }

            var stream = new NativeStream(foreachCount, Allocator.Persistent);
            this.streams.Add(new Tuple<NativeStream, int>(stream, foreachCount));

            return stream.AsWriter();
        }

        /// <summary>
        /// Add a new producer job handle. Can only be called in write mode.
        /// </summary>
        /// <param name="handle">The handle.</param>
        public void AddJobHandleForProducer(JobHandle handle)
        {
            if (!this.producerSafety)
            {
                throw new InvalidOperationException(ProducerException);
            }

            this.producerSafety = false;

            this.AddJobHandleForProducerUnsafe(handle);
        }

        public void AddJobHandleForProducerUnsafe(JobHandle handle)
        {
            if (this.ReadMode)
            {
                throw new InvalidOperationException(WriteModeRequired);
            }

            this.ProducerHandle = JobHandle.CombineDependencies(this.ProducerHandle, handle);
        }

        /// <summary>
        /// Add a new producer job handle. Can only be called in write mode.
        /// </summary>
        /// <param name="handle">The handle.</param>
        public void AddJobHandleForConsumer(JobHandle handle)
        {
            if (!this.consumerSafety)
            {
                throw new InvalidOperationException(ConsumerException);
            }

            this.consumerSafety = false;

            if (!this.ReadMode)
            {
                throw new InvalidOperationException(ReadModeRequired);
            }

            this.ConsumerHandle = JobHandle.CombineDependencies(this.ConsumerHandle, handle);
        }

        /// <summary>
        /// Gets the collection of readers.
        /// </summary>
        /// <returns>Returns a tuple where Item1 is the reader, Item2 is the foreachCount.</returns>
        public IReadOnlyList<Tuple<NativeStream.Reader, int>> GetReaders()
        {
            if (this.consumerSafety)
            {
                throw new InvalidOperationException(ConsumerException);
            }

            this.consumerSafety = true;

            if (!this.ReadMode)
            {
                throw new InvalidOperationException(ReadModeRequired);
            }

            return this.readers;
        }

        /// <summary>
        /// Set the event to read mode.
        /// </summary>
        public void SetReadMode()
        {
            if (this.ReadMode)
            {
                throw new InvalidOperationException(WriteModeRequired);
            }

            this.ReadMode = true;

            for (var index = 0; index < this.streams.Count; index++)
            {
                var stream = this.streams[index];
                this.readers.Add(new Tuple<NativeStream.Reader, int>(stream.Item1.AsReader(), stream.Item2));
            }

            for (var index = 0; index < this.externalReaders.Count; index++)
            {
                var stream = this.externalReaders[index];
                this.readers.Add(new Tuple<NativeStream.Reader, int>(stream.Item1.AsReader(), stream.Item2));
            }
        }

        public void AddReaders(IEnumerable<Tuple<NativeStream, int>> externalStreams)
        {
            if (this.ReadMode)
            {
                throw new InvalidOperationException(WriteModeRequired);
            }

            this.externalReaders.AddRange(externalStreams);
        }

        public void Reset()
        {
            this.ReadMode = false;

            this.streams.Clear();
            this.externalReaders.Clear();
            this.readers.Clear();

            this.ConsumerHandle = default;
            this.ProducerHandle = default;
        }

        public void Dispose()
        {
            for (var index = 0; index < this.streams.Count; index++)
            {
                this.streams[index].Item1.Dispose();
            }
        }
    }
}