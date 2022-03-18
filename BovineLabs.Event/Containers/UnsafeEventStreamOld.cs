// // <copyright file="UnsafeEventStream.cs" company="BovineLabs">
// //     Copyright (c) BovineLabs. All rights reserved.
// // </copyright>
//
// namespace BovineLabs.Event.Containers
// {
//     using System;
//     using System.Diagnostics.CodeAnalysis;
//     using JetBrains.Annotations;
//     using Unity.Burst;
//     using Unity.Collections;
//     using Unity.Collections.LowLevel.Unsafe;
//     using Unity.Jobs;
//     using Unity.Jobs.LowLevel.Unsafe;
//
//     /// <summary>
//     /// A thread data stream supporting parallel reading and parallel writing.
//     /// Allows you to write different types or arrays into a single stream.
//     /// </summary>
//     public unsafe partial struct UnsafeEventStream : IDisposable, IEquatable<UnsafeEventStream>
//     {
//         [NativeDisableUnsafePtrRestriction]
//         private UnsafeEventStreamBlockData* block;
//
//         private Allocator allocator;
//
//         /// <summary> Initializes a new instance of the <see cref="UnsafeEventStream"/> struct. </summary>
//         /// <param name="foreachCount"> The foreach count of the stream. </param>
//         /// <param name="allocator"> The specified type of memory allocation. </param>
//         public UnsafeEventStream(int foreachCount, Allocator allocator)
//         {
//             AllocateBlock(out this, allocator);
//             this.AllocateForEach(foreachCount);
//         }
//
//         /// <summary> Gets a value indicating whether memory for the container is allocated. </summary>
//         /// <value> True if this container object's internal storage has been allocated. </value>
//         /// <remarks>
//         /// <para> Note that the container storage is not created if you use the default constructor.
//         /// You must specify at least an allocation type to construct a usable container. </para>
//         /// </remarks>
//         public bool IsCreated => this.block != null;
//
//         /// <summary> Gets the number of streams the list can use. </summary>
//         public int ForEachCount => block->RangeCount;
//
//         /// <summary> Disposes of this stream and deallocates its memory immediately. </summary>
//         public void Dispose()
//         {
//             this.Deallocate();
//         }
//
//         /// <summary>
//         /// Safely disposes of this container and deallocates its memory when the jobs that use it have completed.
//         /// </summary>
//         /// <remarks>
//         /// <para> You can call this function dispose of the container immediately after scheduling the job. Pass
//         /// the [JobHandle](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html) returned by
//         /// the [Job.Schedule](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobExtensions.Schedule.html)
//         /// method using the `jobHandle` parameter so the job scheduler can dispose the container after all jobs
//         /// using it have run. </para>
//         /// </remarks>
//         /// <param name="dependency"> All jobs spawned will depend on this JobHandle. </param>
//         /// <returns> A new job handle containing the prior handles as well as the handle for the job that deletes
//         /// the container. </returns>
//         public JobHandle Dispose(JobHandle dependency)
//         {
//             var jobHandle = new DisposeJob { Container = this }.Schedule(dependency);
//             this.block = null;
//             return jobHandle;
//         }
//
//         /// <summary> Returns writer instance. </summary>
//         /// <returns> The writer instance. </returns>
//         public Writer AsWriter()
//         {
//             return new Writer(ref this);
//         }
//
//         /// <summary> Returns reader instance. </summary>
//         /// <returns> The reader instance. </returns>
//         public Reader AsReader()
//         {
//             return new Reader(ref this);
//         }
//
//         /// <summary> Compute the item count. </summary>
//         /// <returns> Item count. </returns>
//         public int ComputeItemCount()
//         {
//             var itemCount = 0;
//
//             for (var i = 0; i != this.ForEachCount; i++)
//             {
//                 itemCount += this.block->Ranges[i].ElementCount;
//             }
//
//             return itemCount;
//         }
//
//         /// <inheritdoc/>
//         public bool Equals(UnsafeEventStream other)
//         {
//             return this.block == other.block;
//         }
//
//         /// <inheritdoc/>
//         [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode", Justification = "Only changed when disposed.")]
//         public override int GetHashCode()
//         {
//             return unchecked((int)(long)this.block);
//         }
//
//         /// <summary> Allocate the stream block for data. </summary>
//         /// <param name="stream"> The stream that is being allocated. </param>
//         /// <param name="allocator"> The specified type of memory allocation. </param>
//         internal static void AllocateBlock(out UnsafeEventStream stream, Allocator allocator)
//         {
//             int blockCount = JobsUtility.MaxJobThreadCount;
//
//             int allocationSize = sizeof(UnsafeEventStreamBlockData) + (sizeof(UnsafeEventStreamBlock*) * blockCount);
//             byte* buffer = (byte*)UnsafeUtility.Malloc(allocationSize, 16, allocator);
//             UnsafeUtility.MemClear(buffer, allocationSize);
//
//             var block = (UnsafeEventStreamBlockData*)buffer;
//
//             stream.block = block;
//             stream.allocator = allocator;
//
//             block->Allocator = allocator;
//             block->BlockCount = blockCount;
//             block->Blocks = (UnsafeEventStreamBlock**)(buffer + sizeof(UnsafeEventStreamBlockData));
//
//             block->Ranges = null;
//             block->ThreadRanges = null;
//             block->RangeCount = 0;
//         }
//
//         /// <summary> Allocates the data for each thread based off <see cref="ForEachCount"/> . </summary>
//         internal void AllocateForEach(int forEachCount)
//         {
//             long allocationSize = sizeof(UnsafeEventStreamRange) * forEachCount;
//             this.block->Ranges = (UnsafeEventStreamRange*)UnsafeUtility.Malloc(allocationSize, 16, this.allocator);
//             UnsafeUtility.MemClear(this.block->Ranges, allocationSize);
//
//             long allocationThreadSize = sizeof(UnsafeEventThreadRange) * JobsUtility.MaxJobThreadCount;
//             this.block->ThreadRanges = (UnsafeEventThreadRange*)UnsafeUtility.Malloc(allocationThreadSize, 16, this.allocator);
//             UnsafeUtility.MemClear(this.block->ThreadRanges, allocationThreadSize);
//
//             this.block->RangeCount = forEachCount;
//         }
//
//         private void Deallocate()
//         {
//             if (this.block == null)
//             {
//                 return;
//             }
//
//             for (int i = 0; i != this.block->BlockCount; i++)
//             {
//                 var b = this.block->Blocks[i];
//                 while (b != null)
//                 {
//                     var next = b->Next;
//                     UnsafeUtility.Free(b, this.allocator);
//                     b = next;
//                 }
//             }
//
//             UnsafeUtility.Free(this.block->Ranges, this.allocator);
//             UnsafeUtility.Free(this.block->ThreadRanges, this.allocator);
//             UnsafeUtility.Free(this.block, this.allocator);
//             this.block = null;
//             this.allocator = Allocator.None;
//         }
//
//         /// <summary> The writer instance. </summary>
//         public struct Writer
//         {
//             /// <summary> Gets block stream data. </summary>
//             [NativeDisableUnsafePtrRestriction]
//             internal readonly UnsafeEventStreamBlockData* BlockStream;
//
//             /// <summary> Gets the index of this writer instance. </summary>
//             [NativeSetThreadIndex] // by default use thread index, this can be overridden by using BeginForEachIndex
//             internal int Index;
//
//             [NativeSetThreadIndex]
//             [UsedImplicitly(ImplicitUseKindFlags.Assign)]
//             private int threadIndex;
//
//             /// <summary> Initializes a new instance of the <see cref="Writer"/> struct. </summary>
//             /// <param name="stream"> The stream reference. </param>
//             internal Writer(ref UnsafeEventStream stream)
//             {
//                 this.BlockStream = stream.block;
//                 this.threadIndex = 0; // 0 so main thread works
//                 this.Index = int.MinValue;
//
//                 for (var i = 0; i < JobsUtility.MaxJobThreadCount; i++)
//                 {
//                     this.BlockStream->ThreadRanges[i].ElementCount = 0;
//                     this.BlockStream->ThreadRanges[i].CurrentBlock = null;
//                     this.BlockStream->ThreadRanges[i].CurrentBlockEnd = null;
//                     this.BlockStream->ThreadRanges[i].CurrentPtr = null;
//                     this.BlockStream->ThreadRanges[i].FirstBlock = null;
//                     this.BlockStream->ThreadRanges[i].NumberOfBlocks = 0;
//                     this.BlockStream->ThreadRanges[i].FirstOffset = 0;
//                 }
//
//                 // TODO is needed? memclear outside if it is
//                 for (var i = 0; i < this.BlockStream->RangeCount; i++)
//                 {
//                     this.BlockStream->Ranges[i].ElementCount = 0;
//                     this.BlockStream->Ranges[i].NumberOfBlocks = 0;
//                     this.BlockStream->Ranges[i].OffsetInFirstBlock = 0;
//                     this.BlockStream->Ranges[i].Block = null;
//                     this.BlockStream->Ranges[i].LastOffset = 0;
//                 }
//             }
//
//             /// <summary> Gets the number of streams the container can use. </summary>
//             public int ForEachCount => this.BlockStream->RangeCount;
//
//             /// <summary> Begin writing data at the iteration index. </summary>
//             /// <param name="foreachIndex"> The index to work on. </param>
//             public void BeginForEachIndex(int foreachIndex)
//             {
//                 this.Index = foreachIndex;
//
//                 this.BlockStream->ThreadRanges[this.threadIndex].ElementCount = 0;
//                 this.BlockStream->ThreadRanges[this.threadIndex].NumberOfBlocks = 0;
//                 this.BlockStream->ThreadRanges[this.threadIndex].FirstBlock = this.BlockStream->ThreadRanges[this.threadIndex].CurrentBlock;
//                 this.BlockStream->ThreadRanges[this.threadIndex].FirstOffset = (int)(this.BlockStream->ThreadRanges[this.threadIndex].CurrentPtr -
//                                                                                      (byte*)this.BlockStream->ThreadRanges[this.threadIndex].CurrentBlock);
//             }
//
//             /// <summary> Write data. </summary>
//             /// <param name="value"> The data to write. </param>
//             /// <typeparam name="T"> The type of value. </typeparam>
//             public void Write<T>(T value)
//                 where T : unmanaged
//             {
//                 ref var dst = ref this.Allocate<T>();
//                 dst = value;
//             }
//
//             /// <summary> Allocate space for data. </summary>
//             /// <typeparam name="T"> The type of value. </typeparam>
//             /// <returns> Reference for the allocated space. </returns>
//             public ref T Allocate<T>()
//                 where T : unmanaged
//             {
//                 var size = UnsafeUtility.SizeOf<T>();
// #if UNITY_2020_1_OR_NEWER
//                 return ref UnsafeUtility.AsRef<T>(this.Allocate(size));
// #else
//                 return ref UnsafeUtilityEx.AsRef<T>(this.Allocate(size));
// #endif
//             }
//
//             /// <summary> Allocate space for data. </summary>
//             /// <param name="size"> Size in bytes. </param>
//             /// <returns> Pointer for the allocated space. </returns>
//             public byte* Allocate(int size)
//             {
//                 byte* ptr = this.BlockStream->ThreadRanges[this.threadIndex].CurrentPtr;
//                 this.BlockStream->ThreadRanges[this.threadIndex].CurrentPtr += size;
//
//                 if (this.BlockStream->ThreadRanges[this.threadIndex].CurrentPtr > this.BlockStream->ThreadRanges[this.threadIndex].CurrentBlockEnd)
//                 {
//                     UnsafeEventStreamBlock* oldBlock = this.BlockStream->ThreadRanges[this.threadIndex].CurrentBlock;
//
//                     this.BlockStream->ThreadRanges[this.threadIndex].CurrentBlock = this.BlockStream->Allocate(oldBlock, this.threadIndex);
//                     this.BlockStream->ThreadRanges[this.threadIndex].CurrentPtr = this.BlockStream->ThreadRanges[this.threadIndex].CurrentBlock->Data;
//
//                     if (this.BlockStream->ThreadRanges[this.threadIndex].FirstBlock == null)
//                     {
//                         this.BlockStream->ThreadRanges[this.threadIndex].FirstOffset =
//                             (int)(this.BlockStream->ThreadRanges[this.threadIndex].CurrentPtr -
//                                   (byte*)this.BlockStream->ThreadRanges[this.threadIndex].CurrentBlock);
//
//                         this.BlockStream->ThreadRanges[this.threadIndex].FirstBlock = this.BlockStream->ThreadRanges[this.threadIndex].CurrentBlock;
//                     }
//                     else
//                     {
//                         this.BlockStream->ThreadRanges[this.threadIndex].NumberOfBlocks++;
//                     }
//
//                     this.BlockStream->ThreadRanges[this.threadIndex].CurrentBlockEnd = (byte*)this.BlockStream->ThreadRanges[this.threadIndex].CurrentBlock +
//                                                                                        UnsafeEventStreamBlockData.AllocationSize;
//                     ptr = this.BlockStream->ThreadRanges[this.threadIndex].CurrentPtr;
//                     this.BlockStream->ThreadRanges[this.threadIndex].CurrentPtr += size;
//                 }
//
//                 this.BlockStream->ThreadRanges[this.threadIndex].ElementCount++;
//
//                 this.BlockStream->Ranges[this.Index].ElementCount = this.BlockStream->ThreadRanges[this.threadIndex].ElementCount;
//                 this.BlockStream->Ranges[this.Index].OffsetInFirstBlock = this.BlockStream->ThreadRanges[this.threadIndex].FirstOffset;
//                 this.BlockStream->Ranges[this.Index].Block = this.BlockStream->ThreadRanges[this.threadIndex].FirstBlock;
//
//                 this.BlockStream->Ranges[this.Index].LastOffset = (int)(this.BlockStream->ThreadRanges[this.threadIndex].CurrentPtr -
//                                                                         (byte*)this.BlockStream->ThreadRanges[this.threadIndex].CurrentBlock);
//                 this.BlockStream->Ranges[this.Index].NumberOfBlocks = this.BlockStream->ThreadRanges[this.threadIndex].NumberOfBlocks;
//
//                 return ptr;
//             }
//         }
//
//         /// <summary> The reader instance. </summary>
//         [SuppressMessage("ReSharper", "SA1600", Justification = "Private.")]
//         public struct Reader
//         {
//             [NativeDisableUnsafePtrRestriction]
//             internal readonly UnsafeEventStreamBlockData* BlockStream;
//
//             [NativeDisableUnsafePtrRestriction]
//             internal UnsafeEventStreamBlock* CurrentBlock;
//
//             [NativeDisableUnsafePtrRestriction]
//             internal byte* CurrentPtr;
//
//             [NativeDisableUnsafePtrRestriction]
//             internal byte* CurrentBlockEnd;
//
//             internal int RemainingCount;
//             internal int LastBlockSize;
//
//             /// <summary> Initializes a new instance of the <see cref="Reader"/> struct. </summary>
//             /// <param name="stream"> The stream reference. </param>
//             internal Reader(ref UnsafeEventStream stream)
//             {
//                 this.BlockStream = stream.block;
//                 this.CurrentBlock = null;
//                 this.CurrentPtr = null;
//                 this.CurrentBlockEnd = null;
//                 this.RemainingCount = 0;
//                 this.LastBlockSize = 0;
//             }
//
//             /// <summary> Gets the for each count. </summary>
//             public int ForEachCount => this.BlockStream->RangeCount;
//
//             /// <summary> Gets the remaining item count. </summary>
//             public int RemainingItemCount => this.RemainingCount;
//
//             /// <summary> Begin reading data at the iteration index. </summary>
//             /// <param name="foreachIndex"> The index to start reading. </param>
//             /// <returns> The number of elements at this index. </returns>
//             public int BeginForEachIndex(int foreachIndex)
//             {
//                 this.RemainingCount = this.BlockStream->Ranges[foreachIndex].ElementCount;
//                 this.LastBlockSize = this.BlockStream->Ranges[foreachIndex].LastOffset;
//
//                 this.CurrentBlock = this.BlockStream->Ranges[foreachIndex].Block;
//                 this.CurrentPtr = (byte*)this.CurrentBlock + this.BlockStream->Ranges[foreachIndex].OffsetInFirstBlock;
//                 this.CurrentBlockEnd = (byte*)this.CurrentBlock + UnsafeEventStreamBlockData.AllocationSize;
//
//                 return this.RemainingCount;
//             }
//
//             /// <summary> Returns pointer to data. </summary>
//             /// <param name="size"> The size of the data to read. </param>
//             /// <returns> The pointer to the data. </returns>
//             public byte* ReadUnsafePtr(int size)
//             {
//                 this.RemainingCount--;
//
//                 byte* ptr = this.CurrentPtr;
//                 this.CurrentPtr += size;
//
//                 if (this.CurrentPtr > this.CurrentBlockEnd)
//                 {
//                     this.CurrentBlock = this.CurrentBlock->Next;
//                     this.CurrentPtr = this.CurrentBlock->Data;
//
//                     this.CurrentBlockEnd = (byte*)this.CurrentBlock + UnsafeEventStreamBlockData.AllocationSize;
//
//                     ptr = this.CurrentPtr;
//                     this.CurrentPtr += size;
//                 }
//
//                 return ptr;
//             }
//
//             /// <summary> Read data. </summary>
//             /// <typeparam name="T"> The type of value. </typeparam>
//             /// <returns> The returned data. </returns>
//             public ref T Read<T>()
//                 where T : unmanaged
//             {
//                 int size = UnsafeUtility.SizeOf<T>();
// #if UNITY_2020_1_OR_NEWER
//                 return ref UnsafeUtility.AsRef<T>(this.ReadUnsafePtr(size));
// #else
//                 return ref UnsafeUtilityEx.AsRef<T>(this.ReadUnsafePtr(size));
// #endif
//             }
//
//             /// <summary> Peek into data. </summary>
//             /// <typeparam name="T"> The type of value. </typeparam>
//             /// /// <returns> The returned data. </returns>
//             public ref T Peek<T>()
//                 where T : unmanaged
//             {
//                 int size = UnsafeUtility.SizeOf<T>();
//
//                 byte* ptr = this.CurrentPtr;
//                 if (ptr + size > this.CurrentBlockEnd)
//                 {
//                     ptr = this.CurrentBlock->Next->Data;
//                 }
//
// #if UNITY_2020_1_OR_NEWER
//                 return ref UnsafeUtility.AsRef<T>(ptr);
// #else
//                 return ref UnsafeUtilityEx.AsRef<T>(ptr);
// #endif
//             }
//
//             /// <summary> Compute item count. </summary>
//             /// <returns> Item count. </returns>
//             public int ComputeItemCount()
//             {
//                 int itemCount = 0;
//                 for (int i = 0; i != this.BlockStream->RangeCount; i++)
//                 {
//                     itemCount += this.BlockStream->Ranges[i].ElementCount;
//                 }
//
//                 return itemCount;
//             }
//         }
//
//         [BurstCompile]
//         private struct DisposeJob : IJob
//         {
//             public UnsafeEventStream Container;
//
//             public void Execute()
//             {
//                 this.Container.Deallocate();
//             }
//         }
//     }
// }
