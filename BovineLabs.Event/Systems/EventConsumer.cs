// <copyright file="EventConsumer.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using BovineLabs.Event.Containers;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

    public unsafe struct EventConsumer<T>
        where T : struct
    {
        internal Consumer* consumer;

        public JobHandle GetReaders(JobHandle jobHandle, out NativeArray<NativeEventStream.Reader> readers)
        {
            readers = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<NativeEventStream.Reader>(
                this.consumer->Readers.GetUnsafeList()->Ptr,
                this.consumer->Readers.Length,
                Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref readers, AtomicSafetyHandle.GetTempMemoryHandle());
#endif

            return JobHandle.CombineDependencies(jobHandle, this.consumer->InputHandle);
        }

        public void AddJobHandle(JobHandle handle)
        {
            this.consumer->JobHandle = handle;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.consumer->HandleSet = true;
#endif
        }
    }

    internal struct Consumer
    {
        public UnsafeListPtr<NativeEventStream.Reader> Readers;
        public JobHandle JobHandle;
        public JobHandle InputHandle;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public bool HandleSet;
#endif
    }
}
