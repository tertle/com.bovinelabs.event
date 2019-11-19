namespace BovineLabs.Samples.MultiWorld
{
    using System.Collections.Generic;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// The Updater.
    /// </summary>
    public class Updater : MonoBehaviour
    {
        private List<ComponentSystemBase> updateSystems;
        private List<ComponentSystemBase> fixedSystems;

        public void SetWorlds(World updateWorld, World fixedUpdateWorld)
        {
            this.updateSystems = new List<ComponentSystemBase>(updateWorld.Systems);
            this.fixedSystems = new List<ComponentSystemBase>(fixedUpdateWorld.Systems);
        }

        private void Update()
        {
            // Garbage but can't do anything about it and just for the demo
            foreach (var system in this.updateSystems)
            {
                system.Update();
            }
        }

        private void FixedUpdate()
        {
            // Garbage but can't do anything about it and just for the demo
            foreach (var system in this.fixedSystems)
            {
                system.Update();
            }
        }
    }
}