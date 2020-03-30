// <copyright file="ObjectPool.cs" company="BovineLabs">
// Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Utility
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Very simple object pool implementation.
    /// </summary>
    /// <typeparam name="T"> The type of object to pool. </typeparam>
    public class ObjectPool<T>
    {
        private readonly Stack<T> pool = new Stack<T>();

        private readonly Func<T> create;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectPool{T}"/> class.
        /// </summary>
        /// <param name="create"> The create function. </param>
        public ObjectPool(Func<T> create)
        {
            this.create = create;
        }

        /// <summary>
        /// Get or creates a new instance of T.
        /// </summary>
        /// <returns> The instance. </returns>
        public T Get()
        {
            return this.pool.Count == 0 ? this.create() : this.pool.Pop();
        }

        /// <summary>
        /// Return an item back to the pool.
        /// </summary>
        /// <param name="item"> The item to return. </param>
        public void Return(T item)
        {
            this.pool.Push(item);
        }
    }
}