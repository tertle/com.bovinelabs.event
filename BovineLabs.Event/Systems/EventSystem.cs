// <copyright file="EventSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using Unity.Entities;

    /// <summary> The default EventSystem that updates in the <see cref="LateSimulationSystemGroup"/>. </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class EventSystem : EventSystemBase
    {
    }
}
