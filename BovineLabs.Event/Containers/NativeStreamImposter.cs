// <copyright file="NativeStreamImposter.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary> An imposter class for <see cref="NativeStream"/> to do garbage free comparisons. </summary>
    internal unsafe struct NativeStreamImposter
    {
#pragma warning disable 649
        private readonly void* blockStreamData;
        private Allocator allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle safety;

        [NativeSetClassTypeToNullOnSchedule]
        private DisposeSentinel disposeSentinel;
#endif
#pragma warning restore 649

        public static implicit operator NativeStreamImposter(NativeStream nativeStream)
        {
            return UnsafeUtilityEx.As<NativeStream, NativeStreamImposter>(ref nativeStream);
        }

        public static implicit operator NativeStream(NativeStreamImposter imposter)
        {
            return UnsafeUtilityEx.As<NativeStreamImposter, NativeStream>(ref imposter);
        }

        /// <summary> Compares 2 <see cref="NativeStreamImposter"/> to see if they are the same. </summary>
        /// <param name="other"> The other <see cref="NativeStreamImposter"/> to compare to. </param>
        /// <returns> True if they are the same. </returns>
        public bool Equals(NativeStreamImposter other)
        {
            return this.blockStreamData == other.blockStreamData;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return unchecked((int)(long)this.blockStreamData);
        }
    }
}