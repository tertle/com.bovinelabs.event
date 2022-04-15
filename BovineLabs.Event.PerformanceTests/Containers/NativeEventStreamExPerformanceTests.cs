// <copyright file="NativeEventStreamExPerformanceTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BL_TESTING

namespace BovineLabs.Event.PerformanceTests.Containers
{
    using BovineLabs.Event.Containers;
    using NUnit.Framework;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.PerformanceTesting;

    public class NativeEventStreamExPerformanceTests
    {
        [TestCase(5120)]
        [TestCase(81920)]
        [TestCase(655360)]
        [Performance]
        public unsafe void WriteLarge(int size)
        {
            NativeEventStream stream = default;

            NativeArray<byte> sourceData = new NativeArray<byte>(size, Allocator.TempJob);
            for (var i = 0; i < size; i++)
            {
                sourceData[i] = (byte)(i % 255);
            }

            Measure.Method(() =>
                {
                    NativeEventStream.Writer writer = stream.AsWriter();
                    writer.WriteLarge((byte*)sourceData.GetUnsafeReadOnlyPtr(), size);
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
        public unsafe void WriteLargeBurst(int size)
        {
            NativeEventStream stream = default;

            NativeArray<byte> sourceData = new NativeArray<byte>(size, Allocator.TempJob);
            for (var i = 0; i < size; i++)
            {
                sourceData[i] = (byte)(i % 255);
            }

            Measure.Method(() =>
                {
                    new WriteLargeJob { Writer = stream.AsWriter(), SourceData = sourceData }.Run();
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
        public void Write(int size)
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
        public void WriteBurst(int size)
        {
            NativeEventStream stream = default;

            var sourceData = new NativeArray<byte>(size, Allocator.TempJob);
            for (var i = 0; i < size; i++)
            {
                sourceData[i] = (byte)(i % 255);
            }

            Measure.Method(() =>
                {
                    new WriteJob { Writer = stream.AsWriter(), SourceData = sourceData }.Run();
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
                    var array = new NativeArray<byte>(size, Allocator.Temp);
                    reader.ReadLarge((byte*)array.GetUnsafePtr(), size);
                })
                .SetUp(() =>
                {
                    stream = new NativeEventStream(Allocator.TempJob);
                    var writer = stream.AsWriter();
                    writer.WriteLarge((byte*)sourceData.GetUnsafeReadOnlyPtr(), size);
                })
                .CleanUp(() => stream.Dispose())
                .Run();

            sourceData.Dispose();
        }

        [TestCase(5120)]
        [TestCase(81920)]
        [TestCase(655360)]
        [Performance]
        public unsafe void ReadLargeBurst(int size)
        {
            NativeEventStream stream = default;

            var sourceData = new NativeArray<byte>(size, Allocator.TempJob);
            for (var i = 0; i < size; i++)
            {
                sourceData[i] = (byte)(i % 255);
            }

            Measure.Method(() =>
                {
                    new ReadLargeJob { Reader = stream.AsReader(), Size = size }.Run();
                })
                .SetUp(() =>
                {
                    stream = new NativeEventStream(Allocator.TempJob);
                    var writer = stream.AsWriter();
                    writer.WriteLarge((byte*)sourceData.GetUnsafeReadOnlyPtr(), size);
                })
                .CleanUp(() => stream.Dispose())
                .Run();

            sourceData.Dispose();
        }


        [TestCase(5120)]
        [TestCase(81920)]
        [TestCase(655360)]
        [Performance]
        public void Read(int size)
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

        [TestCase(5120)]
        [TestCase(81920)]
        [TestCase(655360)]
        [Performance]
        public void ReadBurst(int size)
        {
            NativeEventStream stream = default;

            var sourceData = new NativeArray<byte>(size, Allocator.TempJob);
            for (var i = 0; i < size; i++)
            {
                sourceData[i] = (byte)(i % 255);
            }

            Measure.Method(() =>
                {
                    new ReadJob { Reader = stream.AsReader(), Size = size }.Run();
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

        [BurstCompile(CompileSynchronously = true)]
        private unsafe struct WriteLargeJob : IJob
        {
            public NativeEventStream.Writer Writer;

            [ReadOnly]
            public NativeArray<byte> SourceData;

            public void Execute()
            {
                this.Writer.WriteLarge((byte*)this.SourceData.GetUnsafeReadOnlyPtr(), this.SourceData.Length);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct WriteJob : IJob
        {
            public NativeEventStream.Writer Writer;

            [ReadOnly]
            public NativeArray<byte> SourceData;

            public void Execute()
            {
                for (var i = 0; i < SourceData.Length; i++)
                {
                    Writer.Write(SourceData[i]);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private unsafe struct ReadLargeJob : IJob
        {
            [ReadOnly]
            public NativeEventStream.Reader Reader;

            public int Size;

            public void Execute()
            {
                Reader.BeginForEachIndex(0);
                var array = new NativeArray<byte>(Size, Allocator.Temp);
                this.Reader.ReadLarge((byte*)array.GetUnsafePtr(), Size);
            }
        }


        [BurstCompile(CompileSynchronously = true)]
        private struct ReadJob : IJob
        {
            [ReadOnly]
            public NativeEventStream.Reader Reader;

            public int Size;

            public void Execute()
            {
                Reader.BeginForEachIndex(0);

                var nativeArray = new NativeArray<byte>(Size, Allocator.Temp);
                for (var i = 0; i < Size; i++)
                {
                    nativeArray[i] = Reader.Read<byte>();
                }
            }
        }
    }
}

#endif