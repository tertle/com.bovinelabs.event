// <copyright file="SimulationEventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
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

        public NativeQueue<T> CreateEventWriter<T>()
            where T : struct
        {
            return this.EventSystem.CreateEventWriter<T>();
        }

        public void AddJobHandleForProducer<T>(JobHandle handle)
            where T : struct
        {
            this.EventSystem.AddJobHandleForProducer<T>(handle);
        }

        public JobHandle GetEventWriter<T>(JobHandle handle, out NativeStream.Reader stream)
            where T : struct
        {
            return this.EventSystem.GetEventReader<T>(handle, out stream);
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