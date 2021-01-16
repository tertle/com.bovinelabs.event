# Event System
## What is the Event System?
A high performance solution for safely creating events between systems in Unity ECS.

## Features
- High performance
- Same frame producing then consuming
- Share events across worlds
- Any system order
- Support different update rates. e.g. produce in fixed update, consume in update
- Any number of producers and consumers
- Fully threaded
- No sync points
- No garbage
- Easy dependency management
- Safety checks
- Support streams of data

## Producer (Writing)
### Nondetermistic Mode
Provides convenience at the cost of being nondeterministic.

**Entities.ForEach**
```csharp
var writer = this.eventSystem.CreateEventWriter<YourEvent>();

this.Entities.ForEach((Entity entity) =>
	{
		writer.Write(new YourEvent { Entity = entity });
	})
	.ScheduleParallel();

this.eventSystem.AddJobHandleForProducer<YourEvent>(this.Dependency);
```

### Deterministic Mode
Provides determinism by manually handling indexing.

**Entities.ForEach**
```csharp
var writer = this.eventSystem.CreateEventWriter<YourEvent>(this.query.CalculateEntityCount());

this.Entities.ForEach((Entity entity, int entityInQueryIndex) =>
	{
		writer.BeginForEachIndex(entityInQueryIndex);
		writer.Write(new YourEvent { Entity = entity });
		writer.EndForEachIndex();
	})
	.WithStoreEntityQueryInField(ref this.query)
	.ScheduleParallel();

this.eventSystem.AddJobHandleForProducer<YourEvent>(this.Dependency);
```

**IJobChunk**
```csharp
[BurstCompile]
private struct ProducerJob : IJobChunk
{
	[ReadOnly]
	public ArchetypeChunkEntityType EntityTypes;

	public NativeEventStream.IndexWriter Writer;

	public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
	{
		var entities = chunk.GetNativeArray(this.EntityTypes);
		this.Writer.BeginForEachIndex(chunkIndex); // chunkIndex

		for (var i = 0; i < chunk.Count; i++)
		{
			var entity = entities[i];
			this.Writer.Write(new YourEvent { Entity = entity });
		}
		
		this.Writer.EndForEachIndex();
	}
}
```

```csharp
var writer = this.eventSystem.CreateEventWriter<YourEvent>(this.query.CalculateChunkCount()); // ChunkCount

this.Dependency = new ProducerJob
	{
		Writer = writer,
		EntityTypes = this.GetArchetypeChunkEntityType(),
	}
	.ScheduleParallel(this.query, this.Dependency);

this.eventSystem.AddJobHandleForProducer<YourEvent>(this.Dependency);
```

## Consumer (Reading)
Reader is the same regardless of writing mode

### IJobEvent
IJobEvent is the easiest way to read back events when no extra data is being streamed.
To create a job just implement the IJobEvent<T> interface.

```csharp
[BurstCompile]
private struct EventJob : IJobEvent<YourEvent>
{
	public NativeQueue<int>.ParallelWriter Counter;

	public void Execute(YourEvent e)
	{
		// do what you want
	}
}
```

```csharp
var eventSystem = this.World.GetOrCreateSystem<EventSystem>();

this.Dependency = new EventJob
{
	// assign job fields
}
.Schedule<EventJob, YourEvent>(eventSystem);
```

Each reader will be schedule to be read one after the other. Each foreach index is read in parallel.

### IJobEventReaderForEach
Sometimes you need a bit more control over reading as the event system allows streaming of any type of data in your events.
IJobEventReaderForEach gives you direct access to the reader allowing you to read it back in whatever format you desire.
```csharp
[BurstCompile]
private struct EventStreamJob : IJobEventReaderForEach<YourEvent>
{
	public void Execute(NativeEventStream.Reader stream, int foreachIndex)
	{
		var count = stream.BeginForEachIndex(foreachIndex);
		
		for (var i = 0; i < count; i += 4)
		{
			var e = stream.Read<YourEvent>();
			var d1 = stream.Read<YourData1>();
			var d2 = stream.Read<YourData2>();
			var d3 = stream.Read<YourData3>();
		}
	}
}
```

```csharp
var eventSystem = this.World.GetOrCreateSystem<EventSystem>();

this.Dependency = new EventStreamJob
{
	// assign job fields
}
.ScheduleParallel<EventStreamJob, YourEvent>(eventSystem);
```

### IJobEventReader
If you need single thread access to the entire reader, you can instead use IJobEventReader.
```csharp
[BurstCompile]
private struct EventStreamJob : IJobEventReader<YourEvent>
{
	public void Execute(NativeEventStream.Reader stream, int readerIndex)
	{
		for (var foreachIndex = 0; foreachIndex < stream.ForEachCount; foreachIndex++)
		{
			var count = stream.BeginForEachIndex(foreachIndex);
			for (var i = 0; i < count; i++)
			{
				var e = stream.Read<YourEvent>();
			}
		}
	}
}
```

```csharp
var eventSystem = this.World.GetOrCreateSystem<EventSystem>();

this.Dependency = new EventStreamJob
{
	// assign job fields
}
.Schedule<EventStreamJob, YourEvent>(eventSystem);
```

### ConsumeSingleEventSystemBase
If you need to read your events on the main thread you can use ConsumeSingleEventSystemBase.
```csharp
public class MyEventSystem : ConsumeSingleEventSystemBase<MyEvent>
{
	protected override void OnEvent(MyEvent e)
	{

	}
}
```

### ConsumeEventSystemBase
Again if you need a bit more control over reading your events, you can implement ConsumeEventSystemBase instead.
```csharp
public class MyEventSystem : ConsumeEventSystemBase<MyEvent>
{
	protected override void OnEventStream(ref NativeEventStream.Reader reader, int eventCount)
	{
		
	}
}
```

### Manual iteration
// TODO

```csharp
this.Dependency = this.eventSystem.GetEventReaders<T>(this.Dependency, out IReadOnlyList<NativeEventStream.Reader> readers);

this.eventSystem.AddJobHandleForConsumer<T>(this.Dependency);
```

### Extensions
For convenience, a number of common functions and patterns have been included.

To access, use `var extensions = this.World.GetOrCreateSystem<TestEventSystem>().Ex<YourEvent>();`

```csharp
/// <summary> 
/// Ensure a NativeHashMap{TKey,TValue} has the capacity to be filled with all events of a specific type. 
/// </summary>
public JobHandle EnsureHashMapCapacity<TKey, TValue>(JobHandle handle,NativeHashMap<TKey, TValue> hashMap)

/// <summary> Get the total number of events of a specific type. </summary>
public JobHandle GetEventCount(JobHandle handle, NativeArray<int> count)
```

### Other Features

**Checking Event Count**

For performance reasons, you may want to check if any event writers were created this frame, or check how many eventer have been created. This allows exiting out early from an `OnUpdate` without additional processing or scheduling jobs/


* HasEventReaders

*Note: this doesn't check event count just that GetEventWriter<T> was called at some point.*

```csharp
/// <summary> Checks if an event has any readers. </summary>
/// <typeparam name="T"> The event type to check. </typeparam>
/// <returns> True if there are readers for the event. </returns>
World.GetOrCreateSystem<EventSystem>.HasEventReaders<T>()
```

* GetEventCount

```csharp
/// <summary> Get the total number of events of a specific type. </summary>
public JobHandle GetEventCount(JobHandle handle, NativeArray<int> count)
```


// TODO
- **Custom Event Systems**
- **Cross world events**


### Benchmarks
// TODO
