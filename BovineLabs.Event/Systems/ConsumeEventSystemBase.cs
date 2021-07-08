// <copyright file="ConsumeEventSystemBase.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using BovineLabs.Event.Containers;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary> A base system for working with jobs on the main thread. </summary>
    /// <typeparam name="T"> The job type. </typeparam>
    [AlwaysUpdateSystem]
    public abstract class ConsumeEventSystemBase<T> : SystemBase
        where T : struct
    {
        private EventConsumer<T> consumer;
        private EventSystem eventSystem;

        /// <inheritdoc />
        protected sealed override void OnCreate()
        {
            this.eventSystem = this.World.GetOrCreateSystem<EventSystem>();
            this.consumer = this.eventSystem.RegisterConsumer<T>();

            this.Create();
        }

        /// <summary> <see cref="OnCreate"/>. </summary>
        protected virtual void Create()
        {
        }

        /// <inheritdoc />
        protected sealed override void OnDestroy()
        {
            this.eventSystem.DeregisterConsumer(this.consumer);
            this.Destroy();
        }

        /// <summary> <see cref="OnDestroy"/>. </summary>
        protected virtual void Destroy()
        {
        }

        /// <inheritdoc />
        protected sealed override void OnUpdate()
        {
            this.BeforeEvent();

            if (!this.consumer.HasReaders)
            {
                return;
            }

            this.Dependency = this.consumer.GetReaders(this.Dependency, out var readers);
            this.Dependency.Complete();

            try
            {
                for (var i = 0; i < readers.Length; i++)
                {
                    var reader = readers[i];

                    for (var foreachIndex = 0; foreachIndex < reader.ForEachCount; foreachIndex++)
                    {
                        var events = reader.BeginForEachIndex(foreachIndex);
                        this.OnEventStream(ref reader, events);
                        reader.EndForEachIndex();
                    }
                }
            }
            finally
            {
                this.consumer.AddJobHandle(this.Dependency);
            }
        }

        /// <summary> Optional update that can occur before event reading. </summary>
        protected virtual void BeforeEvent()
        {
        }

        /// <summary> A stream of events. </summary>
        /// <param name="reader"> The event stream reader. </param>
        /// <param name="eventCount"> The number of iterations in the stream. </param>
        protected abstract void OnEventStream(ref NativeEventStream.Reader reader, int eventCount);
    }
}