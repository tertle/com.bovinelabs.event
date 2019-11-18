using BovineLabs.Event;
using BovineLabs.Samples;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public class ParallelForProducerSystem : JobComponentSystem
{
    private LateSimulationEventSystem eventSystem;

    protected override void OnCreate()
    {
        this.eventSystem = this.World.GetOrCreateSystem<LateSimulationEventSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle handle)
    {
        const int count = 100;

        var writer = this.eventSystem.CreateEventWriter<TestEventEmpty>(count);

        handle = new ProduceJob
            {
                Events = writer,
                Random = new Random((uint)UnityEngine.Random.Range(0, int.MaxValue)),
            }
            .Schedule(count, 64, handle);

        this.eventSystem.AddJobHandleForProducer<TestEventEmpty>(handle);

        return handle;
    }

    [BurstCompile]
    private struct ProduceJob : IJobParallelFor
    {
        public NativeStream.Writer Events;

        public Random Random;

        public void Execute(int index)
        {
            this.Random.state = (uint)(this.Random.state + index);

            var eventCount = this.Random.NextInt(1, 1000);

            this.Events.BeginForEachIndex(index);
            for (var i = 0; i < eventCount; i++)
            {
                this.Events.Write(default(TestEventEmpty));
            }

            this.Events.EndForEachIndex();
        }
    }
}