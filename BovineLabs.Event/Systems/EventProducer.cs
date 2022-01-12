// <copyright file="EventProducer.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using System;
    using BovineLabs.Event.Containers;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine;

    public unsafe struct EventProducer<T>
        where T : struct
    {
        [NativeDisableUnsafePtrRestriction]
        internal Producer* producer;

        public bool IsValid => this.producer != null;

        /// <summary> Create a new NativeEventStream to write events to. </summary>
        /// <typeparam name="T"> The type of event. </typeparam>
        /// <returns> A <see cref="NativeEventStream.Writer"/> you can write events to. </returns>
        /// <exception cref="InvalidOperationException"> Throw if unbalanced CreateEventWriter and AddJobHandleForProducer calls. </exception>
        public NativeEventStream.Writer CreateWriter()
        {
            Debug.Assert(!this.producer->EventStream.IsCreated, "Creating multiple writers in same frame.");

            var eventStream = new NativeEventStream(Allocator.TempJob);
            this.producer->EventStream = eventStream;
            return eventStream.AsWriter();
        }

        /// <summary>
        /// Create a new NativeEventStream to write events to returning the previous dependency.
        /// You can use ths to write events in a fixed update.
        /// </summary>
        /// <param name="dependency"> The job handle to add. </param>
        /// <param name="writer"> The event writer. </param>
        /// <typeparam name="T"> The type of event. </typeparam>
        /// <returns> A <see cref="NativeEventStream.Writer"/> The new dependency. </returns>
        /// <exception cref="InvalidOperationException"> Throw if unbalanced CreateEventWriter and AddJobHandleForProducer calls. </exception>
        public JobHandle CreateWriter(JobHandle dependency, out NativeEventStream.Writer writer)
        {
            this.producer->JobHandle = JobHandle.CombineDependencies(this.producer->JobHandle, dependency);

            if (this.producer->EventStream.IsCreated)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Debug.Assert(this.producer->HandleSet, "CreateWriter must always be balanced by an AddJobHandle call.");
                this.producer->HandleSet = false;
#endif
                writer = this.producer->EventStream.AsWriter();
                return this.producer->JobHandle;
            }

            var eventStream = new NativeEventStream(Allocator.TempJob);
            this.producer->EventStream = eventStream;
            writer = this.producer->EventStream.AsWriter();
            return this.producer->JobHandle;
        }

        /// <summary> Adds the specified JobHandle to the events list of producer dependency handles. </summary>
        /// <param name="handle"> The job handle to add. </param>
        /// <typeparam name="T"> The type of event to associate the handle to. </typeparam>
        /// <exception cref="InvalidOperationException"> Throw if unbalanced CreateEventWriter and AddJobHandleForProducer calls. </exception>
        public void AddJobHandle(JobHandle handle)
        {
            this.producer->JobHandle = handle;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.producer->HandleSet = true;
#endif
        }
    }

    internal struct Producer
    {
        public NativeEventStream EventStream;
        public JobHandle JobHandle;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public bool HandleSet;
#endif
    }
}
