// <copyright file="EventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using Unity.Entities;

    /// <summary> The default event system that runs in initialization. </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class EventSystem : EventSystemBase
    {
    }
}