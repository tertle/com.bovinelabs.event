// <copyright file="UpdateEventSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples.MultiWorld
{
    using BovineLabs.Event.Systems;
    using Unity.Entities;

    /// <summary>
    /// The UpdateEventSystem.
    /// </summary>
    [DisableAutoCreation]
    public class UpdateEventSystem : EventSystem
    {
        /// <inheritdoc/>
        public override bool UsePersistentAllocator => true;

        /// <inheritdoc/>
        protected override WorldMode Mode => WorldMode.Custom;

        /// <inheritdoc/>
        protected override string CustomKey => "Default World";
    }
}