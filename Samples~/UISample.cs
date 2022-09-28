// <copyright file="UISample.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Samples
{
    using BovineLabs.Event.Samples.MultiWorld;
    using BovineLabs.Event.Systems;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.UI;

    public class UISample : MonoBehaviour
    {
        [Header("Left")]
        [SerializeField]
        private InputField threads = default;

        [SerializeField]
        private InputField writers = default;

        [SerializeField]
        private InputField events = default;

        [SerializeField]
        private Text totalEvents = default;

        [Header("Right")]
        [SerializeField]
        private Text fixedUpdateEvents = default;

        [SerializeField]
        private Text updateEvents = default;

        [SerializeField]
        private Text fixedFramesPerSecondText = default;

        [SerializeField]
        private Text framesPerSecondText = default;

        [SerializeField]
        private float updateInterval = 0.5F;

        private float timeLeft;

        private int fixedCount;
        private int updateCount;
        private int fixedFrames;
        private int updateFrames;

        public void SetFixedUpdate(int i)
        {
            // because we aren't updating this from fixed update, get a bunch of 0s when fixed update hasn't run
            // if we weren't firing events every frame this would produce the wrong result
            // but for this demo we are so should be fine
            if (i == 0)
            {
                return;
            }

            this.fixedCount += i;
            this.fixedFrames++;
        }

        public void SetUpdate(int i)
        {
            this.updateCount += i;
            this.updateFrames++;
        }

        private void Start()
        {
            var world = new World("Standard");
            var standardGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            standardGroup.AddSystemToUpdateList(world.GetOrCreateSystemManaged<UpdateEventSystem>());
            standardGroup.AddSystemToUpdateList(world.GetOrCreateSystemManaged<UpdateEventCounterSystem>());
            standardGroup.AddSystemToUpdateList(world.GetOrCreateSystemManaged<ParallelForProducerSystem>());

            var fixedWorld = new World("Fixed");
            var fixedGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<FixedSystemGroup>();
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(fixedGroup);
            fixedGroup.AddSystemToUpdateList(fixedWorld.GetOrCreateSystemManaged<FixedUpdateEventSystem>());
            fixedGroup.AddSystemToUpdateList(fixedWorld.GetOrCreateSystemManaged<FixedEventCounterSystem>());

            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<EventSystem>().UsePersistentAllocator = true;
        }

        private void Update()
        {
            this.Left();
            this.Right();
        }

        private void Left()
        {
            if (int.TryParse(this.threads.text, out var threadValue) &&
                int.TryParse(this.writers.text, out var writersValue) &&
                int.TryParse(this.events.text, out var eventsValue))
            {
                this.totalEvents.text = (math.abs(threadValue) * math.abs(writersValue) * math.abs(eventsValue)).ToString("N0");

                ParallelForProducerSystem.Threads = math.abs(threadValue);
                ParallelForProducerSystem.Writers = math.abs(writersValue);
                ParallelForProducerSystem.EventsPerThread = math.abs(eventsValue);
            }
        }

        private void Right()
        {
            this.timeLeft -= Time.deltaTime;

            if (this.timeLeft > 0.0)
            {
                return;
            }

            var updateEventsPerFrame = this.updateFrames != 0 ? this.updateCount / this.updateFrames : 0;
            this.updateEvents.text = updateEventsPerFrame.ToString("N0");

            var fixedEventsPerFrame = this.fixedFrames != 0 ? this.fixedCount / this.fixedFrames : 0;
            this.fixedUpdateEvents.text = fixedEventsPerFrame.ToString("N0");

            this.fixedFramesPerSecondText.text = $"{this.fixedFrames / this.updateInterval}";
            this.framesPerSecondText.text = $"{this.updateFrames / this.updateInterval}";

            this.timeLeft = this.updateInterval;
            this.fixedCount = 0;
            this.updateCount = 0;
            this.fixedFrames = 0;
            this.updateFrames = 0;
        }
    }
}
