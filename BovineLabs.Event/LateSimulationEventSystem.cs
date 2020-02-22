// <copyright file="LateSimulationEventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

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