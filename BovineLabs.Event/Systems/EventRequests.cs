// namespace BovineLabs.Event.System2
// {
//     using System;
//     using BovineLabs.Event.Containers;
//     using Unity.Collections;
//     using Unity.Collections.LowLevel.Unsafe;
//     using Unity.Jobs;
//
//     public unsafe struct EventContainerWriter
//     {
//         internal EventContainer* container;
//
//         public NativeEventStream.ThreadWriter CreateEventWriter()
//         {
//             // return container->EventStream.AsThreadWriter();
//         }
//
//         public void AddHandleForProducer(JobHandle handle)
//         {
//             // eventSystem->DependencyHandle = handle;
//         }
//     }
//
//     public unsafe struct EventContainerReader
//     {
//         internal EventContainer* container;
//
//         public NativeEventStream.ThreadWriter CreateEventWriter()
//         {
//             // return container->EventStream.AsThreadWriter();
//         }
//
//         public void AddHandleForProducer(JobHandle handle)
//         {
//             // eventSystem->DependencyHandle = handle;
//         }
//     }
//
//     // public unsafe struct EventSystemWriter
//     // {
//     //     internal EventSystemData* eventSystem;
//     //     internal EventContainer* parentContainer;
//     //
//     //     public EventSystemWriter(EventContainer* parent, Allocator allocator)
//     //     {
//     //         int allocationSize = sizeof(EventSystemData);
//     //         EventSystemData* buffer = (EventSystemData*)UnsafeUtility.Malloc(allocationSize, 16, allocator);
//     //         UnsafeUtility.MemClear(buffer, allocationSize);
//     //
//     //         buffer->EventStream = new NativeEventStream(Allocator.Persistent);
//     //
//     //         this.eventSystem = buffer;
//     //         this.parentContainer = parent;
//     //     }
//     //
//     //     public NativeEventStream.ThreadWriter CreateEventWriter()
//     //     {
//     //         return eventSystem->EventStream.AsThreadWriter();
//     //     }
//     //
//     //     public void AddHandleForProducer(JobHandle handle)
//     //     {
//     //         eventSystem->DependencyHandle = handle;
//     //     }
//     // }
//     //
//     // internal struct EventSystemData : IDisposable
//     // {
//     //     public NativeEventStream EventStream;
//     //     public JobHandle DependencyHandle;
//     //
//     //     public void Dispose()
//     //     {
//     //         this.DependencyHandle.Complete();
//     //         this.EventStream.Dispose();
//     //     }
//     // }
// }