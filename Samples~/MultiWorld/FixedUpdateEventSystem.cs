// <copyright file="FixedUpdateEventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Samples.MultiWorld
{
    using BovineLabs.Event;
    using Unity.Entities;

    [DisableAutoCreation]
    public class FixedUpdateEventSystem : EventSystem
    {
        protected override WorldMode Mode => WorldMode.Active;
    }
}
