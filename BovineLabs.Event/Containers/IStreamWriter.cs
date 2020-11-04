namespace BovineLabs.Event.Containers
{
    public unsafe interface IStreamWriter
    {
        /// <summary> Allocate space for data. </summary>
        /// <param name="size">Size in bytes.</param>
        /// <returns> Reference for the allocated space. </returns>
        byte* Allocate(int size);
    }
}