// <copyright file="NativeStreamImposter.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using JetBrains.Annotations;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe struct NativeStreamImposter
    {
        [UsedImplicitly(ImplicitUseKindFlags.Assign)]
        private readonly void* blockStreamData;

        public static implicit operator NativeStreamImposter(NativeStream nativeStream)
        {
            var ptr = UnsafeUtility.AddressOf(ref nativeStream);
            UnsafeUtility.CopyPtrToStructure(ptr, out NativeStreamImposter imposter);
            return imposter;
        }

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