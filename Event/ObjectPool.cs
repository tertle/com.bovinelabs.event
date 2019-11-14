namespace BovineLabs.Event
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The ObjectPool.
    /// </summary>
    public class ObjectPool<T>
    {
        private Stack<T> pool = new Stack<T>();

        private readonly Func<T> create;

        public ObjectPool(Func<T> create)
        {
            this.create = create;
        }

        public T Get()
        {
            return this.pool.Count == 0 ? this.create() : this.pool.Pop();
        }

        public void Return(T item)
        {
            this.pool.Push(item);
        }
    }
}