// <copyright file="EndFrameEntityEventSystems.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using Unity.Entities;

    /// <summary>
    /// <see cref="EntityEventSystem"/> that runs in the <see cref="LateSimulationSystemGroup"/>.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public sealed class EndSimulationEntityEventSystem : EntityEventSystem
    {
    }

    /// <summary>
    /// <see cref="EntityEventSystem"/> that runs in the <see cref="PresentationSystemGroup"/>.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public sealed class PresentationEntityEventSystem : EntityEventSystem
    {
    }
}