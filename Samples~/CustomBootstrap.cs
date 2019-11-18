namespace BovineLabs.Samples
{
    using System;
    using System.Collections.Generic;
    using BovineLabs.Samples.MultiWorld;
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
            UpdateEventSystem.SetWorld(World.Active);

            var world = new World("Custom World");
            world.GetOrCreateSystem<UpdateEventCounterSystem>();
            world.GetOrCreateSystem<UpdateEventSystem>();

            var fixedWorld = new World("FixedUpdate World");
            fixedWorld.GetOrCreateSystem<FixedEventCounterSystem>();
            fixedWorld.GetOrCreateSystem<FixedUpdateEventSystem>();

            var updater = new GameObject("Updater").AddComponent<Updater>();

            updater.SetWorlds(world, fixedWorld);

            return systems;
        }
    }
}