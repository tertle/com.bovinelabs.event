namespace BovineLabs.Event
{
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    [StructLayout(LayoutKind.Sequential)]
    public struct EventWriter<T>
        where T : struct
    {
        [NativeSetThreadIndex]
        internal int ThreadIndex;

        [WriteOnly]
        internal NativeStream.Writer Stream;

        public void BeginForEachIndex()
        {
            this.Stream.BeginForEachIndex(this.ThreadIndex);
        }

        public void EndForEachIndex()
        {
            this.Stream.EndForEachIndex();
        }

        public void Write(T e)
        {
            this.Stream.Write(e);
        }
    }
}