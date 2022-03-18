// <copyright file="NativeEventStreamEx.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Containers
{
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary> Extensions for NativeEventStream. </summary>
    public static unsafe class NativeEventStreamEx
    {
        /// <summary> Allocate a chunk of memory that can be larger than the max allocation size. </summary>
        /// <param name="writer"> The writer. </param>
        /// <param name="data"> The data to write. </param>
        /// <param name="size"> The size of the data. For an array, this is UnsafeUtility.SizeOf{T} * length. </param>
        /// <typeparam name="T"> StreamWriter. </typeparam>
        public static void AllocateLarge<T>(this ref T writer, byte* data, int size)
            where T : unmanaged, IStreamWriter
        {
            if (size == 0)
            {
                return;
            }

            var maxSize = MaxSize();

            var allocationCount = size / maxSize;
            var allocationRemainder = size % maxSize;

            for (var i = 0; i < allocationCount; i++)
            {
                var ptr = writer.Allocate(maxSize);

                UnsafeUtility.MemCpy(ptr, data + (i * maxSize), maxSize);
            }

            if (allocationRemainder > 0)
            {
                var ptr = writer.Allocate(allocationRemainder);
                UnsafeUtility.MemCpy(ptr, data + (allocationCount * maxSize), allocationRemainder);
            }
        }

        /// <summary> Read a chunk of memory that could have been larger than the max allocation size. </summary>
        /// <param name="reader"> The reader. </param>
        /// <param name="size"> For an array, this is UnsafeUtility.SizeOf{T} * length. </param>
        /// <returns> Pointer to data. </returns>
        public static byte* ReadLarge(this ref NativeEventStream.Reader reader, int size, Allocator allocator = Allocator.Temp)
        {
            if (size == 0)
            {
                return default;
            }

            var maxSize = MaxSize();

            if (size < maxSize)
            {
                return reader.ReadUnsafePtr(size);
            }

            var output = (byte*)UnsafeUtility.Malloc(size, 4, allocator);

            var allocationCount = size / maxSize;
            var allocationRemainder = size % maxSize;

            for (var i = 0; i < allocationCount; i++)
            {
                var ptr = reader.ReadUnsafePtr(maxSize);
                UnsafeUtility.MemCpy(output + (i * maxSize), ptr, maxSize);
            }

            if (allocationRemainder > 0)
            {
                var ptr = reader.ReadUnsafePtr(allocationRemainder);
                UnsafeUtility.MemCpy(output + (allocationCount * maxSize), ptr, allocationRemainder);
            }

            return output;
        }

        private static int MaxSize() => UnsafeEventStreamBlockData.AllocationSize - sizeof(void*);
    }
}
