namespace BovineLabs.Event.Containers
{
    public unsafe interface IStreamWriter
    {
        byte* Allocate(int size);
    }
}