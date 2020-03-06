// <copyright file="EndSimulationEventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using Unity.Entities;

    /// <summary> Event system that runs at end of simulation. </summary>
    /// <remarks>
    /// This is currently not enforced as it just updates in LateSimulationSystemGroup.
    /// If your system is running in LateSimulationGroup you will need to update before this.
    /// </remarks>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public class EndSimulationEventSystem : EventSystem
    {
    }
}