# Event System
## What is the Event System?
A high performance solution for safely creating events between systems in Unity ECS.

## Features
- High performance, negligible overhead
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

## Writing
### Non-determistic Mode
// TODO

### Deterministic Mode
// TODO

## Reading
Reader is the same regardless of writing mode

### IJobEvent and IJobEventStream
// TODO

### ConsumeSingleEventSystemBase and ConsumeEventSystemBase
// TODO

### Manual iteration
// TODO

## Extensions
// TODO