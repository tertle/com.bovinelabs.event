// <copyright file="EventConsumer.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
    using BovineLabs.Event.Containers;
    using Unity.Jobs;

    public unsafe struct EventConsumer<T>
        where T : struct
    {
        internal Consumer* consumer;

        public bool HasReaders()
        {
            return this.consumer->Readers.Length > 0;
        }

        public JobHandle GetReaders(JobHandle jobHandle, out UnsafeListPtr<NativeEventStream.Reader> readers)
        {
            readers = this.consumer->Readers;

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
