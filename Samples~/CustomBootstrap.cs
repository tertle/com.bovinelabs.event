// <copyright file="CustomBootstrap.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples
{
    using BovineLabs.Event.Samples.MultiWorld;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// The CustomBootstrap.
    /// </summary>
    public class CustomBootstrap : ICustomBootstrap
    {
        /// <inheritdoc/>
        public bool Initialize(string defaultWorldName)
        {
            var defaultWorld = new World(defaultWorldName);
            World.DefaultGameObjectInjectionWorld = defaultWorld;

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(defaultWorld, systems);
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(defaultWorld);

            var world = new World("Custom World");
            world.GetOrCreateSystem<UpdateEventCounterSystem>();
            world.GetOrCreateSystem<UpdateEventSystem>();

            var fixedWorld = new World("FixedUpdate World");
            fixedWorld.GetOrCreateSystem<FixedEventCounterSystem>();
            fixedWorld.GetOrCreateSystem<FixedUpdateEventSystem>();

            var updater = new GameObject("Updater").AddComponent<Updater>();

            updater.SetWorlds(world, fixedWorld);

            return true;
        }
    }
}