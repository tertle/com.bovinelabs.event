namespace BovineLabs.Event
{
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe struct NativeStreamImposter
    {
        public void* BlockStreamData;

        public static implicit operator NativeStreamImposter(NativeStream nativeStream)
        {
            var ptr = UnsafeUtility.AddressOf(ref nativeStream);
            UnsafeUtility.CopyPtrToStructure(ptr, out NativeStreamImposter imposter);
            return imposter;
        }

        public bool Equals(NativeStreamImposter other)
        {
            return this.BlockStreamData == other.BlockStreamData;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return unchecked((int)(long)this.BlockStreamData);
        }
    }
}