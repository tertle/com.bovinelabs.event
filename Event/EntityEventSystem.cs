// <copyright file="EntityEventSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;
    using BovineLabs.Common.Jobs;
    using JetBrains.Annotations;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;
    using Unity.Jobs;
    using UnityEngine.Profiling;

    /// <summary>
    /// The BatchBarrierSystem.
    /// </summary>
    [UsedImplicitly]
    public abstract class EntityEventSystem : ComponentSystem
    {
        private readonly Dictionary<Type, IEventBatch> types = new Dictionary<Type, IEventBatch>();

        private JobHandle producerHandle;

        /// <summary>
        /// The interface for the batch systems.
        /// </summary>
        private interface IEventBatch : IDisposable
        {
            /// <summary>
            /// Updates the batch, destroy, create, set.
            /// </summary>
            /// <param name="entityManager">The <see cref="EntityManager"/>.</param>
            /// <returns>A <see cref="JobHandle"/>.</returns>
            JobHandle Update(EntityManager entityManager);

            /// <summary>
            /// Resets the batch for the next frame.
            /// </summary>
            void Reset();
        }

        /// <summary>
        /// Creates a queue where any added component added will be batch created as an entity event
        /// and automatically destroyed 1 frame later.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IComponentData"/>.</typeparam>
        /// <returns>A <see cref="NativeQueue{T}"/> which any component that is added will be turned into a single frame event.</returns>
        public NativeQueue<T> CreateEventQueue<T>()
            where T : struct, IComponentData
        {
            if (!this.types.TryGetValue(typeof(T), out var create))
            {
                create = this.types[typeof(T)] = new EventBatch<T>(this.EntityManager);
            }

            return ((EventBatch<T>)create).GetNew();
        }

        /// <summary>
        /// Add a dependency handle.
        /// </summary>
        /// <param name="handle">The dependency handle.</param>
        public void AddJobHandleForProducer(JobHandle handle)
        {
            this.producerHandle = JobHandle.CombineDependencies(this.producerHandle, handle);
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            foreach (var t in this.types)
            {
                t.Value.Dispose();
            }

            this.types.Clear();
        }

        /// <inheritdoc />
        protected override void OnUpdate()
        {
            this.producerHandle.Complete();
            this.producerHandle = default;

            var handles = new NativeArray<JobHandle>(this.types.Count, Allocator.TempJob);

            int index = 0;

            foreach (var t in this.types)
            {
                handles[index++] = t.Value.Update(this.EntityManager);
            }

            JobHandle.CompleteAll(handles);
            handles.Dispose();

            foreach (var t in this.types)
            {
                t.Value.Reset();
            }
        }

        private class EventBatch<T> : IEventBatch
            where T : struct, IComponentData
        {
            private readonly List<NativeQueue<T>> queues = new List<NativeQueue<T>>();
            private readonly EntityQuery query;

            private readonly EntityArchetype archetype;

            public EventBatch(EntityManager entityManager)
            {
                this.query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<T>());
                this.archetype = entityManager.CreateArchetype(typeof(T));
            }

            public NativeQueue<T> GetNew()
            {
                // Having allocation leak warnings when using TempJob
                var queue = new NativeQueue<T>(Allocator.TempJob);
                this.queues.Add(queue);
                return queue;
            }

            public void Reset()
            {
                foreach (var queue in this.queues)
                {
                    queue.Dispose();
                }

                this.queues.Clear();
            }

            /// <inheritdoc />
            public void Dispose()
            {
                this.Reset();
            }

            /// <summary>
            /// Handles the destroying of entities.
            /// </summary>
            /// <param name="entityManager">The entity manager.</param>
            /// <returns>A default handle.</returns>
            public JobHandle Update(EntityManager entityManager)
            {
                this.DestroyEntities(entityManager);

                if (!this.CreateEntities(entityManager))
                {
                    return default;
                }

                return this.SetComponentData(entityManager);
            }

            private bool CreateEntities(EntityManager entityManager)
            {
                var count = this.GetCount();

                if (count == 0)
                {
                    return false;
                }

                Profiler.BeginSample("CreateEntity");

                // Felt like Temp should be the allocator but gets disposed for some reason.
                using (var entities = new NativeArray<Entity>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
                {
                    entityManager.CreateEntity(this.archetype, entities);
                }

                Profiler.EndSample();
                return true;
            }

            private int GetCount()
            {
                var sum = 0;
                foreach (var i in this.queues)
                {
                    sum += i.Count;
                }

                return sum;
            }

            private JobHandle SetComponentData(EntityManager entityManager)
            {
                var isZeroSized = TypeManager.GetTypeInfo<T>().IsZeroSized;

                if (isZeroSized)
                {
                    return default;
                }

                var componentType = entityManager.GetArchetypeChunkComponentType<T>(false);

                var chunks = this.query.CreateArchetypeChunkArray(Allocator.TempJob);

                int startIndex = 0;

                var handles = new NativeArray<JobHandle>(this.queues.Count, Allocator.TempJob);

                // Create a job for each queue. This is designed so that these jobs can run simultaneously.
                for (var index = 0; index < this.queues.Count; index++)
                {
                    var queue = this.queues[index];
                    var job = new SetComponentDataJob
                    {
                        Chunks = chunks,
                        Queue = queue,
                        StartIndex = startIndex,
                        ComponentType = componentType,
                    };

                    startIndex += queue.Count;

                    handles[index] = job.Schedule();
                }

                var handle = JobHandle.CombineDependencies(handles);
                handles.Dispose();

                // Deallocate the chunk array
                handle = new DeallocateJob<ArchetypeChunk>(chunks).Schedule(handle);

                return handle;
            }

            private void DestroyEntities(EntityManager entityManager)
            {
                Profiler.BeginSample("DestroyEntity");

                entityManager.DestroyEntity(this.query);

                Profiler.EndSample();
            }

            [BurstCompile]
            private struct SetComponentDataJob : IJob
            {
                public int StartIndex;

                public NativeQueue<T> Queue;

                [ReadOnly]
                public NativeArray<ArchetypeChunk> Chunks;

                [NativeDisableContainerSafetyRestriction]
                public ArchetypeChunkComponentType<T> ComponentType;

                /// <inheritdoc />
                public void Execute()
                {
                    this.GetIndexes(out var chunkIndex, out var entityIndex);

                    for (; chunkIndex < this.Chunks.Length; chunkIndex++)
                    {
                        var chunk = this.Chunks[chunkIndex];

                        var components = chunk.GetNativeArray(this.ComponentType);

                        while (this.Queue.TryDequeue(out var item) && entityIndex < components.Length)
                        {
                            components[entityIndex++] = item;
                        }

                        if (this.Queue.Count == 0)
                        {
                            return;
                        }

                        entityIndex = entityIndex < components.Length ? entityIndex : 0;
                    }
                }

                private void GetIndexes(out int chunkIndex, out int entityIndex)
                {
                    var sum = 0;

                    for (chunkIndex = 0; chunkIndex < this.Chunks.Length; chunkIndex++)
                    {
                        var chunk = this.Chunks[chunkIndex];

                        var length = chunk.Count;

                        if (sum + length < this.StartIndex)
                        {
                            sum += length;
                            continue;
                        }

                        entityIndex = this.StartIndex - sum;
                        return;
                    }

                    throw new ArgumentOutOfRangeException(nameof(this.StartIndex));
                }
            }
        }
    }
}