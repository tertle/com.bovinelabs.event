namespace BovineLabs.Samples.MultiWorld
{
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// The Updater.
    /// </summary>
    public class Updater : MonoBehaviour
    {
        private World updateWorld;
        private World fixedUpdateWorld;

        public void SetWorlds(World updateWorld, World fixedUpdateWorld)
        {
            this.updateWorld = updateWorld;
            this.fixedUpdateWorld = fixedUpdateWorld;
        }

        private void Update()
        {
            foreach (var system in this.updateWorld.Systems)
            {
                system.Update();
            }
        }

        private void FixedUpdate()
        {
            /*foreach (var system in this.fixedUpdateWorld.Systems)
            {
                system.Update();
            }*/
        }
    }
}