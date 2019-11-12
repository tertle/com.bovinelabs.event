namespace BovineLabs.Event
{
    using Unity.Entities;
    using UnityEditor;

    /// <summary>
    /// The PresentationEventSystem.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class PresentationEventSystem : ComponentSystem
    {
        private EventSystemImpl eventSystem;
        private SimulationEventSystem simulationEventSystem;

        /// <summary>
        /// Gets the event system to share between simulation and presentation systems.
        /// </summary>
        internal EventSystemImpl EventSystem => this.EventSystem;

        /// <inheritdoc />
        protected override void OnCreate()
        {
            this.simulationEventSystem = this.World.GetExistingSystem<SimulationEventSystem>();

            // Shared event system
            this.eventSystem = this.simulationEventSystem.EventSystem ?? new EventSystemImpl();
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            // SimulationEventSystem handles dispose
            this.eventSystem = null;
        }

        /// <inheritdoc />
        protected override void OnUpdate()
        {
        }
    }
}