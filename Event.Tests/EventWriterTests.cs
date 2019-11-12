namespace BovineLabs.Event.Tests
{
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Jobs;
    using UnityEngine;

    /// <summary>
    /// The EventWriterTests.
    /// </summary>
    public class EventWriterTests
    {
        /// <summary>
        /// Ensures ThreadIndex is actually set automatically by the job system.
        /// </summary>
        [Test]
        public void ThreadIndexSet()
        {
            const int count = 128;

            var stream = new NativeStream(128, Allocator.TempJob);

            var writer = new EventWriter<TestEvent>
            {
                Stream = stream.AsWriter(),
            };

            var output = new NativeHashMap<int, int>(count, Allocator.TempJob);

            new ThreadIndexSetJobTest
                {
                    EventWriter = writer,
                    Output = output.AsParallelWriter(),
                }
                .Schedule(128, 1).Complete();

            var keys = output.GetKeyArray(Allocator.TempJob);

            Assert.IsTrue(keys.Length > 1);
            Debug.Log(keys.Length);

            stream.Dispose();
            keys.Dispose();
            output.Dispose();
        }

        private struct TestEvent
        {
        }

        private struct ThreadIndexSetJobTest : IJobParallelFor
        {
            public EventWriter<TestEvent> EventWriter;

            public NativeHashMap<int, int>.ParallelWriter Output;

            public void Execute(int index)
            {
                this.Output.TryAdd(this.EventWriter.ThreadIndex, 0);
            }
        }
    }
}