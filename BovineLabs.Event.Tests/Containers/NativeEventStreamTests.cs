// <copyright file="NativeEventStreamTests.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if BL_TESTING

namespace BovineLabs.Event.Tests.Containers
{
    using System;
    using BovineLabs.Event.Containers;
    using NUnit.Framework;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;
    using Unity.Entities.Tests;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    /// <summary> Tests for <see cref="NativeEventStream"/> . </summary>
    internal partial class NativeEventStreamTests : ECSTestsFixture
    {
        /// <summary> Tests that you can create and destroy. </summary>
        [Test]
        public void CreateAndDestroy()
        {
            var stream = new NativeEventStream(Allocator.Temp);

            Assert.IsTrue(stream.IsCreated);
            Assert.IsTrue(stream.Count() == 0);

            stream.Dispose();
            Assert.IsFalse(stream.IsCreated);
        }
    }
}

#endif