// <copyright file="EventContainer.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using System;
    using BovineLabs.Event.Containers;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine;

    internal unsafe struct EventContainer : IDisposable
    {
        private UnsafeListPtr<IntPtr> producers;
        private UnsafeListPtr<IntPtr> consumers;

        private UnsafeListPtr<NativeEventStream> currentProducers;

        public EventContainer(long hash)
        {
            this.Hash = hash;
            this.producers = new UnsafeListPtr<IntPtr>(1, Allocator.Persistent);
            this.consumers = new UnsafeListPtr<IntPtr>(1, Allocator.Persistent);
            this.currentProducers = new UnsafeListPtr<NativeEventStream>(1, Allocator.Persistent);
            this.IsValid = true;
        }

        public long Hash { get; }

        public bool IsValid { get; private set; }

        public void Dispose()
        {
            for (var i = this.producers.Length - 1; i >= 0; i--)
            {
                this.RemoveProducerInternal((Producer*)this.producers[i]);
            }

            this.producers.Dispose();

            for (var i = this.consumers.Length - 1; i >= 0; i--)
            {
                this.RemoveConsumerInternal((Consumer*)this.consumers[i]);
            }

            this.consumers.Dispose();

            for (var i = 0; i < this.currentProducers.Length; i++)
            {
                this.currentProducers[i].Dispose();
            }

            this.currentProducers.Dispose();

            this.IsValid = false;
        }

        public EventProducer<T> CreateProducer<T>()
            where T : unmanaged
        {
            var producer = (Producer*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<Producer>(), UnsafeUtility.AlignOf<Producer>(), Allocator.Persistent);
            UnsafeUtility.MemClear(producer, UnsafeUtility.SizeOf<Producer>());

            this.producers.Add((IntPtr)producer);

            return new EventProducer<T> { Producer = producer };
        }

        public EventConsumer<T> CreateConsumer<T>()
            where T : unmanaged
        {
            var consumer = (Consumer*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<Consumer>(), UnsafeUtility.AlignOf<Consumer>(), Allocator.Persistent);
            UnsafeUtility.MemClear(consumer, UnsafeUtility.SizeOf<Consumer>());
            consumer->Readers = new UnsafeListPtr<NativeEventStream>(0, Allocator.Persistent);

            this.consumers.Add((IntPtr)consumer);

            return new EventConsumer<T> { Consumer = consumer };
        }

        public void RemoveProducer<T>(EventProducer<T> producer)
            where T : unmanaged
        {
            this.RemoveProducerInternal(producer.Producer);
        }

        public void RemoveConsumer<T>(EventConsumer<T> consumer)
            where T : unmanaged
        {
            this.RemoveConsumerInternal(consumer.Consumer);
        }

        public void Update()
        {
            var producerHandle = this.GetProducerHandle();
            var consumerHandle = this.GetConsumerHandle();

            // Dispose all current producers when all the consumers are finished
            for (var i = 0; i < this.currentProducers.Length; i++)
            {
                this.currentProducers[i].Dispose(consumerHandle);
            }

            this.currentProducers.Clear();

            // Grab all new producers and reset them
            for (var i = 0; i < this.producers.Length; i++)
            {
                var producer = (Producer*)this.producers[i];
                if (producer->EventStream.IsCreated)
                {
                    this.currentProducers.Add(producer->EventStream);
                }

                producer->EventStream = default;
                producer->JobHandle = default;
            }

            // Dispose our previous consumers and assign producers to our consumers
            for (var i = 0; i < this.consumers.Length; i++)
            {
                var consumer = (Consumer*)this.consumers[i];
                consumer->Readers.Clear();
                consumer->InputHandle = producerHandle;
                consumer->JobHandle = default;

                for (var r = 0; r < this.currentProducers.Length; r++)
                {
                    consumer->Readers.Add(this.currentProducers[r]);
                }
            }
        }

        private JobHandle GetProducerHandle()
        {
            var handles = new NativeList<JobHandle>(this.producers.Length, Allocator.Temp);

            for (var i = 0; i < this.producers.Length; i++)
            {
                var producer = (Producer*)this.producers[i];

                if (!producer->EventStream.IsCreated)
                {
                    continue;
                }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!producer->HandleSet)
                {
                    Debug.LogError("CreateWriter must always be balanced by an AddJobHandle call.");
                    continue;
                }

                producer->HandleSet = false;
#endif

                handles.Add(producer->JobHandle);
            }

            return JobHandle.CombineDependencies(handles.AsArray());
        }

        private JobHandle GetConsumerHandle()
        {
            var handles = new NativeList<JobHandle>(this.consumers.Length, Allocator.Temp);

            for (var i = 0; i < this.consumers.Length; i++)
            {
                var consumer = (Consumer*)this.consumers[i];

                if (!consumer->Readers.IsCreated)
                {
                    continue;
                }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (consumer->ReadersRequested != consumer->HandleSet)
                {
                    Debug.LogError("GetReaders must always be balanced by an AddJobHandle call.");
                    continue;
                }

                consumer->HandleSet = 0;
                consumer->ReadersRequested = 0;
#endif

                handles.Add(consumer->JobHandle);
            }

            return JobHandle.CombineDependencies(handles.AsArray());
        }

        private void RemoveProducerInternal(Producer* producer)
        {
            for (var i = 0; i < this.producers.Length; i++)
            {
                if (this.producers[i] == (IntPtr)producer)
                {
                    if (producer->EventStream.IsCreated)
                    {
                        producer->EventStream.Dispose();
                    }

                    this.producers.RemoveAtSwapBack(i);
                    break;
                }
            }

            UnsafeUtility.Free(producer, Allocator.Persistent);
        }

        private void RemoveConsumerInternal(Consumer* consumer)
        {
            for (var i = 0; i < this.consumers.Length; i++)
            {
                if (this.consumers[i] == (IntPtr)consumer)
                {
                    if (consumer->Readers.IsCreated)
                    {
                        consumer->Readers.Dispose();
                    }

                    this.consumers.RemoveAtSwapBack(i);
                    break;
                }
            }

            UnsafeUtility.Free(consumer, Allocator.Persistent);
        }
    }
}
