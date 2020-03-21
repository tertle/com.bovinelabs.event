// <copyright file="NativeThreadStream.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine.Assertions;

    [SuppressMessage("ReSharper", "SA1600", Justification = "TODO LATER")]
    [SuppressMessage("ReSharper", "SA1642", Justification = "TODO LATER")]
    [SuppressMessage("ReSharper", "SA1623", Justification = "TODO LATER")]
    [SuppressMessage("ReSharper", "SA1614", Justification = "TODO LATER")]
    [SuppressMessage("ReSharper", "SA1615", Justification = "TODO LATER")]
    [SuppressMessage("ReSharper", "SA1611", Justification = "TODO LATER")]
    [NativeContainer]
    public unsafe struct NativeThreadStream<T> : IDisposable
        where T : unmanaged
    {
        private UnsafeThreadStream stream;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
        private AtomicSafetyHandle m_Safety;

        [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
        [NativeSetClassTypeToNullOnSchedule]
        private DisposeSentinel m_DisposeSentinel;
#endif

        public NativeThreadStream(Allocator allocator)
        {
            AllocateBlock(out this, allocator);
            this.stream.AllocateForEach();
        }

        /// <summary>
        /// Reports whether memory for the container is allocated.
        /// </summary>
        /// <value>True if this container object's internal storage has been allocated.</value>
        /// <remarks>Note that the container storage is not created if you use the default constructor. You must specify
        /// at least an allocation type to construct a usable container.</remarks>
        public bool IsCreated => this.stream.IsCreated;

        public Writer AsWriter()
        {
            return new Writer(ref this);
        }

        public Reader AsReader()
        {
            return new Reader(ref this);
        }

        /// <summary>
        /// Compute item count.
        /// </summary>
        /// <returns>Item count.</returns>
        public int ComputeItemCount()
        {
            this.CheckReadAccess();
            return this.stream.ComputeItemCount();
        }

        /// <summary>
        /// Copies stream data into NativeArray.
        /// </summary>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        /// <returns>A new NativeArray, allocated with the given strategy and wrapping the stream data.</returns>
        /// <remarks>The array is a copy of stream data.</remarks>
        public NativeArray<T> ToNativeArray(Allocator allocator)
        {
            var array = new NativeArray<T>(this.ComputeItemCount(), allocator, NativeArrayOptions.UninitializedMemory);
            var reader = this.AsReader();

            int offset = 0;
            for (var i = 0; i != UnsafeThreadStream.ForEachCount; i++)
            {
                reader.BeginForEachIndex(i);
                int rangeItemCount = reader.RemainingItemCount;
                for (int j = 0; j < rangeItemCount; ++j)
                {
                    array[offset] = reader.Read();
                    offset++;
                }
            }

            return array;
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref this.m_Safety, ref this.m_DisposeSentinel);
#endif
            this.stream.Dispose();
        }

        private static void AllocateBlock(out NativeThreadStream<T> stream, Allocator allocator)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (allocator <= Allocator.None)
            {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", "allocator");
            }
#endif
            UnsafeThreadStream.AllocateBlock(out stream.stream, allocator);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out stream.m_Safety, out stream.m_DisposeSentinel, 0, allocator);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
#endif
        }

        [NativeContainer]
        [NativeContainerSupportsMinMaxWriteRestriction]
        public struct Writer
        {
            private UnsafeThreadStream.Writer writer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
            [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
            private AtomicSafetyHandle m_Safety;
#endif

            internal Writer(ref NativeThreadStream<T> stream)
            {
                this.writer = stream.stream.AsWriter();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.m_Safety = stream.m_Safety;
#endif
            }

            /// <summary>
            /// Write data.
            /// </summary>
            public void Write(T value)
            {
                ref T dst = ref this.Allocate();
                dst = value;
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            public ref T Allocate()
            {
                CollectionHelper.CheckIsUnmanaged<T>();
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtilityEx.AsRef<T>(this.Allocate(size));
            }

            /// <summary>
            /// Allocate space for data.
            /// </summary>
            /// <param name="size">Size in bytes.</param>
            public byte* Allocate(int size)
            {
                this.AllocateChecks(size);
                return this.writer.Allocate(size);
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local", Justification = "Intentional")]
            private void AllocateChecks(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);

                if (size > UnsafeThreadStreamBlockData.AllocationSize - sizeof(void*))
                {
                    throw new ArgumentException("Allocation size is too large");
                }
#endif
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        public struct Reader
        {
            private UnsafeThreadStream.Reader reader;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            private int remainingBlocks;
            [SuppressMessage("ReSharper", "SA1308", Justification = "Required by safety injection.")]
            [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Required by safety injection.")]
            private AtomicSafetyHandle m_Safety;
#endif

            internal Reader(ref NativeThreadStream<T> stream)
            {
                this.reader = stream.stream.AsReader();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.remainingBlocks = 0;
                this.m_Safety = stream.m_Safety;
#endif
            }

            /// <summary>
            /// Returns remaining item count.
            /// </summary>
            public int RemainingItemCount => this.reader.RemainingItemCount;

            /// <summary>
            /// Begin reading data at the iteration index.
            /// </summary>
            /// <param name="foreachIndex"></param>
            /// <remarks>BeginForEachIndex must always be called balanced by a EndForEachIndex.</remarks>
            /// <returns>The number of elements at this index.</returns>
            public int BeginForEachIndex(int foreachIndex)
            {
                this.BeginForEachIndexChecks(foreachIndex);

                var remainingItemCount = this.reader.BeginForEachIndex(foreachIndex);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                this.remainingBlocks = this.reader.BlockStream->Ranges[foreachIndex].NumberOfBlocks;
                if (this.remainingBlocks == 0)
                {
                    this.reader.CurrentBlockEnd = (byte*)this.reader.CurrentBlock + this.reader.LastBlockSize;
                }
#endif

                return remainingItemCount;
            }

            /// <summary>
            /// Returns pointer to data.
            /// </summary>
            public byte* ReadUnsafePtr(int size)
            {
                this.ReadChecks(size);

                this.reader.RemainingCount--;

                byte* ptr = this.reader.CurrentPtr;
                this.reader.CurrentPtr += size;

                if (this.reader.CurrentPtr > this.reader.CurrentBlockEnd)
                {
                    this.reader.CurrentBlock = this.reader.CurrentBlock->Next;
                    this.reader.CurrentPtr = this.reader.CurrentBlock->Data;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    this.remainingBlocks--;

                    if (this.remainingBlocks < 0)
                    {
                        throw new System.ArgumentException("Reading out of bounds");
                    }

                    if (this.remainingBlocks == 0 && size + sizeof(void*) > this.reader.LastBlockSize)
                    {
                        throw new System.ArgumentException("Reading out of bounds");
                    }

                    if (this.remainingBlocks <= 0)
                    {
                        this.reader.CurrentBlockEnd = (byte*)this.reader.CurrentBlock + this.reader.LastBlockSize;
                    }
                    else
                    {
                        this.reader.CurrentBlockEnd = (byte*)this.reader.CurrentBlock + UnsafeThreadStreamBlockData.AllocationSize;
                    }
#else
                    this.reader.CurrentBlockEnd = (byte*)this.reader.CurrentBlock + UnsafeThreadStreamBlockData.AllocationSize;
#endif
                    ptr = this.reader.CurrentPtr;
                    this.reader.CurrentPtr += size;
                }

                return ptr;
            }

            /// <summary> Read data. </summary>
            public ref T Read()
            {
                int size = UnsafeUtility.SizeOf<T>();
                return ref UnsafeUtilityEx.AsRef<T>(this.ReadUnsafePtr(size));
            }

            /// <summary> Peek into data. </summary>
            public ref T Peek()
            {
                int size = UnsafeUtility.SizeOf<T>();
                this.ReadChecks(size);

                return ref this.reader.Peek<T>();
            }

            /// <summary>
            /// Compute item count.
            /// </summary>
            /// <returns>Item count.</returns>
            public int ComputeItemCount()
            {
                this.CheckAccess();
                return this.reader.ComputeItemCount();
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckAccess()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local", Justification = "Intentional")]
            private void ReadChecks(int size)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);

                Assert.IsTrue(size <= UnsafeThreadStreamBlockData.AllocationSize - sizeof(void*));
                if (this.reader.RemainingCount < 1)
                {
                    throw new ArgumentException("There are no more items left to be read.");
                }
#endif
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void BeginForEachIndexChecks(int forEachIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);

                if (forEachIndex < 0 || forEachIndex >= UnsafeThreadStream.ForEachCount)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(forEachIndex), $"foreachIndex: {forEachIndex} must be between 0 and ForEachCount: {UnsafeThreadStream.ForEachCount}");
                }
#endif
            }
        }
    }
}