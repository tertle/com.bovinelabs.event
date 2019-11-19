// <copyright file="UIOutput.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Samples
{
    using UnityEngine;
    using UnityEngine.UI;

    public class UIOutput : MonoBehaviour
    {
        [SerializeField]
        private Text fixedUpdateEvents = default;

        [SerializeField]
        private Text updateEvents = default;

        [SerializeField]
        private Text fixedFramesPerSecondText = default;

        [SerializeField]
        private Text framesPerSecondText = default;

        [SerializeField] private float updateInterval = 0.5F;

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

        private void Update()
        {
            this.timeLeft -= Time.deltaTime;

            if (this.timeLeft > 0.0)
            {
                return;
            }

            var updateEventsPerFrame = this.updateFrames != 0 ? this.updateCount / this.updateFrames : 0;
            this.updateEvents.text = updateEventsPerFrame.ToString();

            var fixedEventsPerFrame = this.fixedFrames != 0 ? this.fixedCount / this.fixedFrames : 0;
            this.fixedUpdateEvents.text = fixedEventsPerFrame.ToString();

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