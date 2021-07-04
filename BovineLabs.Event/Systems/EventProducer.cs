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
        internal Producer* producer;

        public bool IsValid => this.producer != null;

        /// <summary> Create a new NativeEventStream in thread mode for writing events to. </summary>
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

        internal void Dispose()
        {
            UnsafeUtility.Free(this.producer, Allocator.Persistent);
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
