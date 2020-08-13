namespace BovineLabs.Event.PerformanceTests.Containers
{
    using BovineLabs.Event.Containers;
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.PerformanceTesting;

    public class NativeEventStreamExPerformanceTests
    {
        [TestCase(5120)]
        [TestCase(81920)]
        [TestCase(655360)]
        [Performance]
        public unsafe void AllocateLarge(int size)
        {
            NativeEventStream stream = default;

            var sourceData = new NativeArray<byte>(size, Allocator.TempJob);
            for (var i = 0; i < size; i++)
            {
                sourceData[i] = (byte)(i % 255);
            }

            Measure.Method(() =>
                {
                    var writer = stream.AsWriter();
                    writer.AllocateLarge((byte*)sourceData.GetUnsafeReadOnlyPtr(), size);
                })
                .SetUp(() => { stream = new NativeEventStream(Allocator.TempJob); })
                .CleanUp(() => stream.Dispose())
                .Run();

            sourceData.Dispose();
        }

        [TestCase(5120)]
        [TestCase(81920)]
        [TestCase(655360)]
        [Performance]
        public void AllocateArray(int size)
        {
            NativeEventStream stream = default;

            var sourceData = new NativeArray<byte>(size, Allocator.TempJob);
            for (var i = 0; i < size; i++)
            {
                sourceData[i] = (byte)(i % 255);
            }

            Measure.Method(() =>
                {
                    var writer = stream.AsWriter();

                    for (var i = 0; i < size; i++)
                    {
                        writer.Write(sourceData[i]);
                    }
                })
                .SetUp(() => { stream = new NativeEventStream(Allocator.TempJob); })
                .CleanUp(() => stream.Dispose())
                .Run();

            sourceData.Dispose();
        }

        [TestCase(5120)]
        [TestCase(81920)]
        [TestCase(655360)]
        [Performance]
        public unsafe void ReadLarge(int size)
        {
            NativeEventStream stream = default;

            var sourceData = new NativeArray<byte>(size, Allocator.TempJob);
            for (var i = 0; i < size; i++)
            {
                sourceData[i] = (byte)(i % 255);
            }

            Measure.Method(() =>
                {
                    var reader = stream.AsReader();
                    reader.BeginForEachIndex(0);
                    var ptr = reader.ReadLarge(size);
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(ptr, size, Allocator.None);
                })
                .SetUp(() =>
                {
                    stream = new NativeEventStream(Allocator.TempJob);
                    var writer = stream.AsWriter();
                    writer.AllocateLarge((byte*)sourceData.GetUnsafeReadOnlyPtr(), size);
                })
                .CleanUp(() => stream.Dispose())
                .Run();

            sourceData.Dispose();
        }

        [TestCase(5120)]
        [TestCase(81920)]
        [TestCase(655360)]
        [Performance]
        public void ReadArray(int size)
        {
            NativeEventStream stream = default;

            var sourceData = new NativeArray<byte>(size, Allocator.TempJob);
            for (var i = 0; i < size; i++)
            {
                sourceData[i] = (byte)(i % 255);
            }

            Measure.Method(() =>
                {
                    var reader = stream.AsReader();
                    reader.BeginForEachIndex(0);

                    var nativeArray = new NativeArray<byte>(size, Allocator.Temp);
                    for (var i = 0; i < size; i++)
                    {
                        nativeArray[i] = reader.Read<byte>();
                    }
                })
                .SetUp(() =>
                {
                    stream = new NativeEventStream(Allocator.TempJob);
                    var writer = stream.AsWriter();
                    for (var i = 0; i < size; i++)
                    {
                        writer.Write(sourceData[i]);
                    }
                })
                .CleanUp(() => stream.Dispose())
                .Run();

            sourceData.Dispose();
        }
    }
}