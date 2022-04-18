// <copyright file="UIOutputSystem.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples
{
    using BovineLabs.Event.Samples.Events;
    using BovineLabs.Event.Systems;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// The UIOutputSystem.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public class UIOutputSystem : ComponentSystem
    {
        private UISample uiSample;
        private EventSystem eventSystem;

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            this.eventSystem = this.World.GetExistingSystem<EventSystem>();
            this.uiSample = Object.FindObjectOfType<UISample>();

            if (this.uiSample == null)
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
            Debug.Log("UISample not found in scene. Load Scenes/SampleScene");
            this.Enabled = false;
        }

        private void UpdateFixedEvents()
        {
            if (this.uiSample == null)
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

            this.uiSample.SetFixedUpdate(fixedEvents);
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

            this.uiSample.SetUpdate(events);
        }
    }
}