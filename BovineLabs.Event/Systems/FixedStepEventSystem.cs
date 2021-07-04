// <copyright file="FixedStepEventSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using Unity.Entities;

    /// <summary> EventSystem that updates in the <see cref="FixedStepSimulationSystemGroup"/>. </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public class FixedStepEventSystem : EventSystemBase
    {
    }
}
