namespace BovineLabs.Event.Systems
{
    using System.Collections.Generic;
    using BovineLabs.Event.Containers;
    using Unity.Entities;

    /// <summary> A base system for working with jobs on the main thread. </summary>
    /// <typeparam name="T"> The job type. </typeparam>
    public abstract class EventSystemBase<T> : SystemBase
        where T : unmanaged
    {
        private EndSimulationEventSystem eventSystem;

        protected abstract int ElementsPerEvent { get; }

        /// <inheritdoc />
        protected sealed override void OnCreate()
        {
            this.eventSystem = this.World.GetOrCreateSystem<EndSimulationEventSystem>();

            this.Create();
        }

        protected virtual void Create()
        {

        }

        /// <inheritdoc />
        protected sealed override void OnDestroy()
        {
            this.Destroy();
        }

        protected virtual void Destroy()
        {

        }

        /// <inheritdoc />
        protected sealed override void OnUpdate()
        {

            this.BeforeEvents();

            if (!this.eventSystem.HasEventReaders<T>())
            {
                return;
            }

            this.Dependency = this.eventSystem.GetEventReaders<T>(this.Dependency, out IReadOnlyList<NativeThreadStream.Reader> readers);
            this.Dependency.Complete();

            try
            {
                for (var i = 0; i < readers.Count; i++)
                {
                    var reader = readers[i];

                    for (var foreachIndex = 0; foreachIndex < reader.ForEachCount; foreachIndex++)
                    {
                        var events = reader.BeginForEachIndex(foreachIndex);
                        for (var j = 0; j < events; j += this.ElementsPerEvent)
                        {
                            this.HandleEvent(ref reader);
                        }

                        reader.EndForEachIndex();
                    }
                }
            }
            finally
            {
                this.eventSystem.AddJobHandleForConsumer<T>(this.Dependency);
            }
        }

        protected virtual void BeforeEvents()
        {
        }

        protected abstract void HandleEvent(ref NativeThreadStream.Reader reader);
    }
}