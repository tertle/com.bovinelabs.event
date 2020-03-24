// <copyright file="Updater.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples.MultiWorld
{
    using Unity.Entities;
    using UnityEngine;

    /// <summary> The Updater. </summary>
    public class Updater : MonoBehaviour
    {
        private World.NoAllocReadOnlyCollection<ComponentSystemBase> updateSystems;
        private World.NoAllocReadOnlyCollection<ComponentSystemBase> fixedSystems;

        public void SetWorlds(World updateWorld, World fixedUpdateWorld)
        {
            this.updateSystems = updateWorld.Systems;
            this.fixedSystems = fixedUpdateWorld.Systems;
        }

        private void Update()
        {
            // Garbage but can't do anything about it and just for the demo
            for (var index = 0; index < this.updateSystems.Count; index++)
            {
                var system = this.updateSystems[index];
                system.Update();
            }
        }

        private void FixedUpdate()
        {
            // Garbage but can't do anything about it and just for the demo
            for (var index = 0; index < this.fixedSystems.Count; index++)
            {
                var system = this.fixedSystems[index];
                system.Update();
            }
        }
    }
}