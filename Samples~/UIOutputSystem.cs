// <copyright file="UIOutputSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples
{
    using BovineLabs.Event;
    using BovineLabs.Event.Systems;
    using BovineLabs.Events.Samples.Events;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// The UIOutputSystem.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PresentationEventSystem))]
    public class UIOutputSystem : ComponentSystem
    {
        private UIOutput uiOutput;
        private PresentationEventSystem eventSystem;

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            this.eventSystem = this.World.GetOrCreateSystem<PresentationEventSystem>();
            this.uiOutput = Object.FindObjectOfType<UIOutput>();

            if (this.uiOutput == null)
            {
                this.Disable();
            }
        }

        /// <inheritdoc/>
        protected override void OnUpdate()
        {
            this.UpdateUpdateEvents();
            this.UpdateFixedEvents();
        }

        private void Disable()
        {
            Debug.Log("UIOutput not found in scene. Load Scenes/SampleScene");
            this.Enabled = false;
        }

        private void UpdateFixedEvents()
        {
            if (this.uiOutput == null)
            {
                this.Disable();
                return;
            }

            var handle = this.eventSystem.GetEventReaders<FixedUpdateCountEvent>(default, out var readers);
            this.eventSystem.AddJobHandleForConsumer<FixedUpdateCountEvent>(handle); // necessary evil
            handle.Complete();

            int fixedEvents = 0;

            for (var i = 0; i < readers.Count; i++)
            {
                var reader = readers[i];

                for (var j = 0; j < reader.ForEachCount; j++)
                {
                    var count = reader.BeginForEachIndex(j);
                    for (var k = 0; k < count; k++)
                    {
                        fixedEvents += reader.Read<FixedUpdateCountEvent>().Value;
                    }

                    reader.EndForEachIndex();
                }
            }

            this.uiOutput.SetFixedUpdate(fixedEvents);
        }

        private void UpdateUpdateEvents()
        {
            var handle = this.eventSystem.GetEventReaders<UpdateCountEvent>(default, out var readers);
            handle.Complete();
            this.eventSystem.AddJobHandleForConsumer<UpdateCountEvent>(default); // necessary evil

            int events = 0;

            for (var i = 0; i < readers.Count; i++)
            {
                var reader = readers[i];

                for (var j = 0; j < reader.ForEachCount; j++)
                {
                    var count = reader.BeginForEachIndex(j);
                    for (var k = 0; k < count; k++)
                    {
                        events += reader.Read<UpdateCountEvent>().Value;
                    }

                    reader.EndForEachIndex();
                }
            }

            this.uiOutput.SetUpdate(events);
        }
    }
}