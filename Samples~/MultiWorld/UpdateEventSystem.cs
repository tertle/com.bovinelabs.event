// <copyright file="UpdateEventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Samples.MultiWorld
{
    using BovineLabs.Event;
    using Unity.Entities;

    /// <summary>
    /// The UpdateEventSystem.
    /// </summary>
    [DisableAutoCreation]
    public class UpdateEventSystem : EventSystem
    {
        /// <inheritdoc/>
        protected override WorldMode Mode => WorldMode.Custom;

        /// <inheritdoc/>
        protected override string CustomKey => "Default World";
    }
}