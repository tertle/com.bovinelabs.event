namespace BovineLabs.Event.Samples.MultiWorld
{
    using Unity.Entities;
    using UnityEngine.Scripting;

    public class FixedSystemGroup : ComponentSystemGroup
    {
        [Preserve]
        public FixedSystemGroup()
        {
            int count = -1;

            this.UpdateCallback = group =>
            {
                if (count == 3)
                {
                    count = -1;
                    return true;
                }

                count++;
                return false;
            };
        }
    }
}