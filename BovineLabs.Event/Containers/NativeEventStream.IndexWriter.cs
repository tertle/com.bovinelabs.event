// namespace BovineLabs.Event.Containers
// {
//     using System;
//     using System.Diagnostics;
//     using System.Diagnostics.CodeAnalysis;
//     using JetBrains.Annotations;
//     using Unity.Assertions;
//     using Unity.Collections.LowLevel.Unsafe;
//
//     public unsafe partial struct NativeEventStream
//     {
//         /// <summary> The writer instance. </summary>
//         [NativeContainer]
//         [NativeContainerSupportsMinMaxWriteRestriction]
//         [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection and being consistent.")]
//         [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection and being consistent.")]
//         public struct IndexWriter
//         {
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//             [UsedImplicitly(ImplicitUseKindFlags.Assign)]
//             private AtomicSafetyHandle m_Safety;
// #pragma warning disable 414
//             private int m_Length;
// #pragma warning restore 414
//             private int m_MinIndex;
//             private int m_MaxIndex;
//
//             [NativeDisableUnsafePtrRestriction]
//             private void* m_PassByRefCheck;
// #endif
//             private UnsafeEventStreamNew.IndexWriter m_Writer;
//
//             /// <summary> Initializes a new instance of the <see cref="IndexWriter"/> struct. </summary>
//             /// <param name="stream"> The stream reference. </param>
//             internal IndexWriter(ref UnsafeEventStreamNew stream)
//             {
//                 this.m_Writer = stream.stream.AsIndexWriter();
//
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//                 this.m_Safety = stream.m_Safety;
//                 this.m_Length = int.MaxValue;
//                 this.m_MinIndex = int.MinValue;
//                 this.m_MaxIndex = int.MinValue;
//                 this.m_PassByRefCheck = null;
// #endif
//                 if (stream.useThreads)
//                 {
//                     this.m_Writer.Index = 0; // for main thread
//                 }
//             }
//
//             /// <summary> Gets the number of streams the container can use. </summary>
//             public int ForEachCount
//             {
//                 get
//                 {
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//                     AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);
// #endif
//                     return this.m_Writer.ForEachCount;
//                 }
//             }
//
//             public void PatchMinMaxRange(int foreEachIndex)
//             {
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//                 this.m_MinIndex = foreEachIndex;
//                 this.m_MaxIndex = foreEachIndex;
// #endif
//             }
//
//             /// <summary> Begin reading data at the iteration index. </summary>
//             /// <param name="foreachIndex"> The index. </param>
//             public void BeginForEachIndex(int foreachIndex)
//             {
//                 this.BeginForEachIndexChecks(foreachIndex);
//                 this.m_Writer.BeginForEachIndex(foreachIndex);
//             }
//
//             /// <summary> Write data. </summary>
//             /// <param name="value"> The data to write. </param>
//             /// <typeparam name="T"> The type of value. </typeparam>
//             public void Write<T>(T value)
//                 where T : struct
//             {
//                 ref T dst = ref this.Allocate<T>();
//                 dst = value;
//             }
//
//             /// <summary> Allocate space for data. </summary>
//             /// <typeparam name="T"> The type of value. </typeparam>
//             /// <returns> Reference for the allocated space. </returns>
//             public ref T Allocate<T>()
//                 where T : struct
//             {
//                 int size = UnsafeUtility.SizeOf<T>();
//
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
//                 this.AllocateChecks(size);
//                 return this.m_Writer.Allocate(size);
//             }
//
//             [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
//             void CheckBeginForEachIndex(int foreachIndex)
//             {
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//                 AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
//
//                 if (m_PassByRefCheck == null)
//                 {
//                     m_PassByRefCheck = UnsafeUtility.AddressOf(ref this);
//                 }
//
//                 if (foreachIndex < m_MinIndex || foreachIndex > m_MaxIndex)
//                 {
//                     // When the code is not running through the job system no ParallelForRange patching will occur
//                     // We can't grab m_BlockStream->RangeCount on creation of the writer because the RangeCount can be initialized
//                     // in a job after creation of the writer
//                     if (m_MinIndex == int.MinValue && m_MaxIndex == int.MinValue)
//                     {
//                         m_MinIndex = 0;
//                         m_MaxIndex = m_Writer.BlockStream->RangeCount - 1;
//                     }
//
//                     if (foreachIndex < m_MinIndex || foreachIndex > m_MaxIndex)
//                     {
//                         throw new ArgumentException($"Index {foreachIndex} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in NativeStream.");
//                     }
//                 }
//
//                 if (this.m_Writer.m_ForeachIndex != int.MinValue)
//                 {
//                     throw new ArgumentException($"BeginForEachIndex must always be balanced by a EndForEachIndex call");
//                 }
//
//                 if (0 != this.m_Writer.BlockStream->Ranges[foreachIndex].ElementCount)
//                 {
//                     throw new ArgumentException($"BeginForEachIndex can only be called once for the same index ({foreachIndex}).");
//                 }
//
//                 Assert.IsTrue(foreachIndex >= 0 && foreachIndex < this.m_Writer.BlockStream->RangeCount);
// #endif
//             }
//
//             [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
//             [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local", Justification = "Intentional")]
//             private void AllocateChecks(int size)
//             {
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//                 AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);
//
//                 // This is for thread version which doesn't call BeginForEachIndexChecks
//                 if (this.m_PassByRefCheck == null)
//                 {
//                     this.m_PassByRefCheck = UnsafeUtility.AddressOf(ref this);
//                 }
//                 else if (this.m_PassByRefCheck != UnsafeUtility.AddressOf(ref this))
//                 {
//                     throw new ArgumentException("NativeEventStream.Writer must be passed by ref once it is in use");
//                 }
//
//                 if (!this.m_UseThreads && this.m_Writer.Index == int.MinValue)
//                 {
//                     throw new ArgumentException("BeginForEachIndex must be called before Allocate");
//                 }
//
//                 if (size > UnsafeEventStreamBlockData.AllocationSize - sizeof(void*))
//                 {
//                     throw new ArgumentException("Allocation size is too large");
//                 }
// #endif
//             }
//         }
//     }
// }