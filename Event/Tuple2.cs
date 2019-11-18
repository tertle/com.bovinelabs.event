namespace BovineLabs.Event
{
    /// <summary>
    /// A simple value tuple that does not generate garbage on GetHashCode.
    /// </summary>
    public struct Tuple2<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;

        public Tuple2(T1 item1, T2 item2)
        {
            this.Item1 = item1;
            this.Item2 = item2;
        }
    }
}