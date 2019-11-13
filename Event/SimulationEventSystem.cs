// <copyright file="SimulationEventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    /// The SimulationEventSystem.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class SimulationEventSystem : JobComponentSystem
    {
        private PresentationEventSystem presentationEventSystem;

        private JobHandle producerHandle;

        /// <summary>
        /// Gets the event system to share between simulation and presentation systems.
        /// </summary>
        internal EventSystemImpl EventSystem { get; private set; }

        public NativeStream.Writer CreateEventWriter<T>(int forEachCount)
            where T : struct
        {
            return this.EventSystem.CreateEventWriter<T>(forEachCount);
        }

        public void AddJobHandleForProducer<T>(JobHandle handle)
            where T : struct
        {
            this.EventSystem.AddJobHandleForProducer<T>(handle);
        }

        public JobHandle GetEventReaders<T>(JobHandle handle, out IReadOnlyList<ValueTuple<NativeStream.Reader, int>> readers)
            where T : struct
        {
            return JobHandle.CombineDependencies(this.EventSystem.GetEventReaders<T>(out readers), handle);
        }

        public void AddJobHandleForConsumer(JobHandle handle)
        {
            this.EventSystem.AddJobHandleForConsumer(handle);
        }

        /// <inheritdoc />
        protected override void OnCreate()
        {
            this.presentationEventSystem = this.World.GetOrCreateSystem<PresentationEventSystem>();

            // Shared event system
            this.EventSystem = this.presentationEventSystem.EventSystem ?? new EventSystemImpl();
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            this.EventSystem.Dispose();
            this.EventSystem = null;
        }

        protected override JobHandle OnUpdate(JobHandle handle)
        {
            return this.EventSystem.OnUpdate(handle);
        }
    }
}