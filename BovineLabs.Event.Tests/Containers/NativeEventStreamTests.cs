namespace BovineLabs.Event.Tests.Containers
{
    using System;
    using BovineLabs.Event.Containers;
    using NUnit.Framework;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using UnityEngine;

    internal class NativeEventStreamTests
    {
        [BurstCompile(CompileSynchronously = true)]
        private struct WriteInts : IJobParallelFor
        {
            public NativeEventStream<int>.Writer Writer;

            public void Execute(int index)
            {
                this.Writer.BeginForEachIndex(index);
                for (int i = 0; i != index; i++)
                {
                    this.Writer.Write(i);
                }

                this.Writer.EndForEachIndex();
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct ReadInts : IJobParallelFor
        {
            public NativeEventStream<int>.Reader Reader;

            public void Execute(int index)
            {
                int count = this.Reader.BeginForEachIndex(index);
                Assert.AreEqual(count, index);

                for (int i = 0; i != index; i++)
                {
                    Assert.AreEqual(index - i, this.Reader.RemainingItemCount);
                    var peekedValue = this.Reader.Peek();
                    var value = this.Reader.Read();
                    Assert.AreEqual(i, value);
                    Assert.AreEqual(i, peekedValue);
                }

                this.Reader.EndForEachIndex();
            }
        }

        [Test]
        public void PopulateInts([Values(1, 3, 10)] int batchSize)
        {
            var count = JobsUtility.MaxJobThreadCount;

            var stream = new NativeEventStream<int>(Allocator.TempJob);
            var fillInts = new WriteInts { Writer = stream.AsWriter() };
            var jobHandle = fillInts.Schedule(count, batchSize);

            var compareInts = new ReadInts { Reader = stream.AsReader() };
            var res0 = compareInts.Schedule(count, batchSize, jobHandle);
            var res1 = compareInts.Schedule(count, batchSize, jobHandle);

            res0.Complete();
            res1.Complete();

            stream.Dispose();
        }

        [Test]
        public void CreateAndDestroy()
        {
            var stream = new NativeEventStream<int>(Allocator.Temp);

            Assert.IsTrue(stream.IsCreated);
            Assert.IsTrue(stream.ComputeItemCount() == 0);

            stream.Dispose();
            Assert.IsFalse(stream.IsCreated);
        }

        [Test]
        public void ItemCount([Values(1, 3, 10)] int batchSize)
        {
            var count = JobsUtility.MaxJobThreadCount;

            var stream = new NativeEventStream<int>(Allocator.TempJob);
            var fillInts = new WriteInts { Writer = stream.AsWriter() };
            fillInts.Schedule(count, batchSize).Complete();

            Assert.AreEqual(count * (count - 1) / 2, stream.ComputeItemCount());

            stream.Dispose();
        }

        [Test]
        public void DisposeJob()
        {
            var stream = new NativeEventStream<int>(Allocator.TempJob);
            Assert.IsTrue(stream.IsCreated);

            var fillInts = new WriteInts { Writer = stream.AsWriter() };
            var writerJob = fillInts.Schedule(JobsUtility.MaxJobThreadCount, 16);

            var disposeJob = stream.Dispose(writerJob);
            Assert.IsFalse(stream.IsCreated);

            disposeJob.Complete();
        }


#if ENABLE_UNITY_COLLECTIONS_CHECKS

        [Test]
        public void OutOfBoundsWriteThrows()
        {
            var stream = new NativeEventStream<int>(Allocator.Temp);
            var writer = stream.AsWriter();
            Assert.Throws<ArgumentException>(() => writer.BeginForEachIndex(-1));
            Assert.Throws<ArgumentException>(() => writer.BeginForEachIndex(JobsUtility.MaxJobThreadCount));

            stream.Dispose();
        }

        [Test]
        public void EndForEachIndexWithoutBeginThrows()
        {
            var stream = new NativeEventStream<int>(Allocator.Temp);
            var writer = stream.AsWriter();
            Assert.Throws<ArgumentException>(() => writer.EndForEachIndex());

            stream.Dispose();
        }

        [Test]
        public void WriteWithoutBeginThrows()
        {
            var stream = new NativeEventStream<int>(Allocator.Temp);
            var writer = stream.AsWriter();
            Assert.Throws<ArgumentException>(() => writer.Write(5));

            stream.Dispose();
        }

        [Test]
        public void WriteAfterEndThrows()
        {
            var stream = new NativeEventStream<int>(Allocator.Temp);
            var writer = stream.AsWriter();
            writer.BeginForEachIndex(0);
            writer.Write(2);
            writer.EndForEachIndex();

            Assert.Throws<ArgumentException>(() => writer.Write(5));

            stream.Dispose();
        }

        [Test]
        public void UnbalancedBeginThrows()
        {
            var stream = new NativeEventStream<int>(Allocator.Temp);
            var writer = stream.AsWriter();
            writer.BeginForEachIndex(0);
            // Missing EndForEachIndex();
            Assert.Throws<ArgumentException>(() => writer.BeginForEachIndex(1));

            stream.Dispose();
        }

        static void CreateBlockStream1And2Int(out NativeEventStream<int> stream)
        {
            stream = new NativeEventStream<int>(Allocator.Temp);

            var writer = stream.AsWriter();
            writer.BeginForEachIndex(0);
            writer.Write(0);
            writer.EndForEachIndex();

            writer.BeginForEachIndex(1);
            writer.Write(1);
            writer.Write(2);
            writer.EndForEachIndex();
        }

        [Test]
        public void ReadWithoutBeginThrows()
        {
            NativeEventStream<int> stream;
            CreateBlockStream1And2Int(out stream);

            var reader = stream.AsReader();
            Assert.Throws<ArgumentException>(() => reader.Read());

            stream.Dispose();
        }

        [Test]
        public void TooManyReadsThrows()
        {
            NativeEventStream<int> stream;
            CreateBlockStream1And2Int(out stream);

            var reader = stream.AsReader();

            reader.BeginForEachIndex(0);
            reader.Read();
            Assert.Throws<ArgumentException>(() => reader.Read());

            stream.Dispose();
        }

        [Test]
        public void CopyWriterByValueThrows()
        {
            var stream = new NativeEventStream<int>(Allocator.Temp);
            var writer = stream.AsWriter();

            writer.BeginForEachIndex(0);

            Assert.Throws<ArgumentException>(() =>
            {
                var writerCopy = writer;
                writerCopy.Write(5);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var writerCopy = writer;
                writerCopy.BeginForEachIndex(1);
                writerCopy.Write(5);
            });

            stream.Dispose();
        }

        [Test]
        public void WriteSameIndexTwiceThrows()
        {
            var stream = new NativeEventStream<int>(Allocator.Temp);
            var writer = stream.AsWriter();

            writer.BeginForEachIndex(0);
            writer.Write(1);
            writer.EndForEachIndex();

            Assert.Throws<ArgumentException>(() =>
            {
                writer.BeginForEachIndex(0);
                writer.Write(2);
            });

            stream.Dispose();
        }
#endif
    }
}