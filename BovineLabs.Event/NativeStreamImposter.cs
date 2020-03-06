// <copyright file="NativeStreamImposter.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary>
    /// An imposter class for <see cref="NativeStream"/> to do garbage free comparisons.
    /// </summary>
    public unsafe struct NativeStreamImposter
    {
#pragma warning disable 649
        private readonly void* blockStreamData;
#pragma warning restore 649

        public static implicit operator NativeStreamImposter(NativeStream nativeStream)
        {
            var ptr = UnsafeUtility.AddressOf(ref nativeStream);
            UnsafeUtility.CopyPtrToStructure(ptr, out NativeStreamImposter imposter);
            return imposter;
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