// <copyright file="NativeStreamImposter.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Data
{
    using JetBrains.Annotations;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary> An imposter class for <see cref="NativeStream"/> to do garbage free comparisons. </summary>
    public unsafe struct NativeStreamImposter
    {
#pragma warning disable 649
        private readonly void* blockStreamData;
        private readonly Allocator allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private readonly AtomicSafetyHandle safety;

        [NativeSetClassTypeToNullOnSchedule]
        private readonly DisposeSentinel disposeSentinel;
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

        /// <summary> An imposter class for <see cref="NativeStream.Reader"/>. </summary>
        public struct Reader
        {
            [UsedImplicitly]
            private fixed byte bytes[64]; // UnsafeUtility.SizeOf<NativeStream.Reader>()

            public static implicit operator NativeStreamImposter.Reader(NativeStream.Reader nativeStream)
            {
                return UnsafeUtilityEx.As<NativeStream.Reader, NativeStreamImposter.Reader>(ref nativeStream);
            }

            public static implicit operator NativeStream.Reader(NativeStreamImposter.Reader imposter)
            {
                return UnsafeUtilityEx.As<NativeStreamImposter.Reader, NativeStream.Reader>(ref imposter);
            }

            public NativeStream.Reader AsReader()
            {
                return this;
            }
        }
    }
}