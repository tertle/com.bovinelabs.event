// <copyright file="FixedUpdateEventSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples.MultiWorld
{
    using BovineLabs.Event.Systems;
    using Unity.Entities;

    /// <summary>
    /// The FixedUpdateEventSystem.
    /// </summary>
    [DisableAutoCreation]
    public class FixedUpdateEventSystem : EventSystem
    {
        /// <inheritdoc/>
        protected override WorldMode Mode => WorldMode.DefaultWorldName;
    }
}
