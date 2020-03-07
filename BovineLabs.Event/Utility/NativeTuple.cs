// <copyright file="Tuple.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event
{
    using System;

    /// <summary> A simple blittable tuple that does not generate garbage on GetHashCode. </summary>
    /// <typeparam name="T1">Type of first item.</typeparam>
    /// <typeparam name="T2">Type of second item.</typeparam>
    public struct NativeTuple<T1, T2> : IEquatable<Tuple<T1, T2>>
        where T1 : struct
        where T2 : struct
    {
        /// <summary> Initializes a new instance of the <see cref="NativeTuple{T1,T2}"/> struct. </summary>
        /// <param name="item1"> The first item. </param>
        /// <param name="item2"> The second item. </param>
        public NativeTuple(T1 item1, T2 item2)
        {
            this.Item1 = item1;
            this.Item2 = item2;
        }

        /// <summary> Gets the first item. </summary>
        public T1 Item1 { get; }

        /// <summary> Gets the second item. </summary>
        public T2 Item2 { get; }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Item1.GetHashCode() * 397) ^ this.Item2.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <inheritdoc/>
        public bool Equals(Tuple<T1, T2> other)
        {
            return this.Item1.Equals(other.Item1) && this.Item2.Equals(other.Item2);
        }
    }
}