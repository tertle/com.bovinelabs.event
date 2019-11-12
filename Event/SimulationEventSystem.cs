// <copyright file="SimulationEventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    /// The SimulationEventSystem.
    /// </summary>
    public class SimulationEventSystem : ComponentSystem
    {
        private PresentationEventSystem presentationEventSystem;
        private EventSystemImpl eventSystem;

        private JobHandle producerHandle;

        /// <summary>
        /// Gets the event system to share between simulation and presentation systems.
        /// </summary>
        internal EventSystemImpl EventSystem => this.EventSystem;

        public JobHandle CombineInputHandle<T>(JobHandle handle)
            where T : struct
        {
            var e = this.eventSystem.GetOrCreateEventContainer<T>();
            return JobHandle.CombineDependencies(handle, e.Handle);
        }

        public EventWriter<T> GetEventWriter<T>(ComponentSystemBase system)
            where T : struct
        {
            return this.eventSystem.GetEventWriter<T>(system);
        }

        public void AddJobHandleForProducer<T>(JobHandle handle)
            where T : struct
        {
            this.eventSystem.AddJobHandleForProducer<T>(handle);
        }

        /// <inheritdoc />
        protected override void OnCreate()
        {
            this.presentationEventSystem = this.World.GetOrCreateSystem<PresentationEventSystem>();

            // Shared event system
            this.eventSystem = this.presentationEventSystem.EventSystem ?? new EventSystemImpl();
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            this.eventSystem.Dispose();
            this.eventSystem = null;
        }

        /// <inheritdoc/>
        protected override void OnUpdate()
        {
        }
    }
}