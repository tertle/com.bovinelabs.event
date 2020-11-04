namespace BovineLabs.Event.Samples.MultiWorld
{
    using Unity.Entities;
    using UnityEngine.Scripting;

    public class FixedSystemGroup : ComponentSystemGroup
    {
        [Preserve]
        public FixedSystemGroup()
        {
#if UNITY_ENTITIES_0_16_OR_NEWER
            this.FixedRateManager = new UpdateTimeFour();
#else
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
#endif

        }

#if UNITY_ENTITIES_0_16_OR_NEWER
        public class UpdateTimeFour : IFixedRateManager
        {
            private int count = -1;

            public bool ShouldGroupUpdate(ComponentSystemGroup @group)
            {
                if (this.count == 3)
                {
                    this.count = -1;
                    return true;
                }

                this.Timestep = group.Time.DeltaTime * 4;

                this.count++;
                return false;
            }

            public float Timestep { get; set; }
        }
#endif
    }
}