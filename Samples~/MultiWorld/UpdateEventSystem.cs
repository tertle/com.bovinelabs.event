// <copyright file="UpdateEventSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Samples.MultiWorld
{
    using BovineLabs.Event;
    using Unity.Entities;

    [DisableAutoCreation]
    public class UpdateEventSystem : EventSystem
    {
        private static World customWorld;

        protected override WorldMode Mode => WorldMode.Custom;

        protected override World CustomWorld => customWorld;

        public static void SetWorld(World world)
        {
            customWorld = world;
        }
    }
}