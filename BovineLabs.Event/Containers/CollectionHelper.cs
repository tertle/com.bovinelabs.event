namespace BovineLabs.Event.Containers
{
    using System;
    using System.Diagnostics;
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;

    public static class CollectionHelper
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [BurstDiscard] // Must use BurstDiscard because UnsafeUtility.IsUnmanaged is not burstable.
        internal static void CheckIsUnmanaged<T>()
        {
            if (!UnsafeUtility.IsValidNativeContainerElementType<T>())
            {
                throw new ArgumentException($"{typeof(T)} used in native collection is not blittable, not primitive, or contains a type tagged as NativeContainer");
            }
        }
    }
}