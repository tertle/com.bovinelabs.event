// <copyright file="PresentationEventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using Unity.Entities;

    /// <summary>
    /// The LateSimulationEventSystem.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class PresentationEventSystem : EventSystem
    {
    }
}