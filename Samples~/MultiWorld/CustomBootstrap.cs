namespace BovineLabs.Samples.MultiWorld
{
    using System;
    using System.Collections.Generic;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// The CustomBootstrap.
    /// </summary>
    public class CustomBootstrap : ICustomBootstrap
    {
        /// <inheritdoc/>
        public List<Type> Initialize(List<Type> systems)
        {
            CustomWorldEventSystem.SetWorld(World.Active);

            var world = new World("Custom World");
            world.GetOrCreateSystem<EventCounterSystem<CustomWorldEventSystem>>();
            world.GetOrCreateSystem<CustomWorldEventSystem>();

            var fixedWorld = new World("FixedUpdate World");
            fixedWorld.GetOrCreateSystem<EventCounterSystem<ActiveWorldEventSystem>>();
            fixedWorld.GetOrCreateSystem<ActiveWorldEventSystem>();

            var updater = new GameObject("Updater").AddComponent<Updater>();

            updater.SetWorlds(world, fixedWorld);

            return systems;
        }
    }
}