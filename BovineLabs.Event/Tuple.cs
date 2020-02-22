// <copyright file="Tuple.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using System;

    /// <summary>
    /// A simple value tuple that does not generate garbage on GetHashCode.
    /// </summary>
    /// <typeparam name="T1">Type of first item.</typeparam>
    /// <typeparam name="T2">Type of second item.</typeparam>
    public struct Tuple<T1, T2> : IEquatable<Tuple<T1, T2>>
        where T1 : struct
        where T2 : struct
    {
        public Tuple(T1 item1, T2 item2)
        {
            this.Item1 = item1;
            this.Item2 = item2;
        }

        public T1 Item1 { get; }

        public T2 Item2 { get; }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Item1.GetHashCode() * 397) ^ this.Item2.GetHashCode();
            }
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public bool Equals(Tuple<T1, T2> other)
        {
            return this.Item1.Equals(other.Item1) && this.Item2.Equals(other.Item2);
        }
    }
}