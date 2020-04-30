namespace BovineLabs.Event.Systems
{
    using BovineLabs.Event.Containers;

    /// <summary> A base system for working with jobs that have no extra data. </summary>
    /// <typeparam name="T"> The job type. </typeparam>
    public abstract class SingleEventSystemBase<T> : EventSystemBase<T>
        where T : unmanaged
    {
        protected abstract void HandleEvent(T e);

        /// <inheritdoc/>
        protected sealed override int ElementsPerEvent => 1;

        /// <inheritdoc/>
        protected sealed override void HandleEvent(ref NativeThreadStream.Reader reader)
        {
            var e = reader.Read<T>();
            this.HandleEvent(e);
        }
    }
}