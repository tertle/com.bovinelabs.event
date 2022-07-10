// <copyright file="NativeEventStreamReaderTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BL_TESTING

namespace BovineLabs.Event.Tests.Containers
{
    using System;
    using BovineLabs.Event.Containers;
    using BovineLabs.Testing;
    using NUnit.Framework;
    using Unity.Collections;
    using Unity.Jobs.LowLevel.Unsafe;

    internal partial class NativeEventStreamTests
    {
        internal class Reader : ECSTestsFixture
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            /// <summary> Ensures that reading with begin throws an exception. </summary>
            [Test]
            public void ReadWithoutBeginThrows()
            {
                var stream = new NativeEventStream(Allocator.Temp);
                stream.AsWriter().Write(0);

                var reader = stream.AsReader();
                Assert.Throws<ArgumentException>(() => reader.Read<int>());
            }

            /// <summary> Ensures that begin reading out of range throws an exception. </summary>
            [Test]
            public void BeginOutOfRangeThrows()
            {
                var stream = new NativeEventStream(Allocator.Temp);

                var reader = stream.AsReader();
                Assert.Throws<ArgumentOutOfRangeException>(() => reader.BeginForEachIndex(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    reader.BeginForEachIndex(JobsUtility.MaxJobThreadCount + 1));
            }

            /// <summary> Ensures reading past the end throws an exception. </summary>
            [Test]
            public void TooManyReadsThrows()
            {
                var stream = new NativeEventStream(Allocator.Temp);
                stream.AsWriter().Write(0);

                var reader = stream.AsReader();
                reader.BeginForEachIndex(0);
                reader.Read<int>();
                Assert.Throws<ArgumentException>(() => reader.Read<int>());
            }
#endif
        }
    }
}

#endif
