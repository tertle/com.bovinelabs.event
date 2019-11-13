namespace BovineLabs.Event
{
    using Unity.Entities;

    /// <summary>
    /// The LateSimulationEventSystem.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class LateSimulationEventSystem : EventSystem
    {
    }
}