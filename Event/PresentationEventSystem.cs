namespace BovineLabs.Event
{
    using Unity.Entities;

    /// <summary>
    /// The PresentationEventSystem.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class PresentationEventSystem : ComponentSystem
    {
        //private EventSystem simulationEventSystem;

        /// <summary>
        /// Gets the event system to share between simulation and presentation systems.
        /// </summary>
        //internal EventSystemImpl EventSystem { get; private set; }

        /// <inheritdoc />
        protected override void OnCreate()
        {
            /*this.simulationEventSystem = this.World.GetOrCreateSystem<EventSystem>();

            // Shared event system
            this.EventSystem = this.simulationEventSystem.EventSystem ?? new EventSystemImpl();*/
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            // EventSystem handles dispose
            //this.EventSystem = null;
        }

        /// <inheritdoc />
        protected override void OnUpdate()
        {
        }
    }
}