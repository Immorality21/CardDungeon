public static class ByteExtension
{
    public static bool IsBitSet(this byte b, int pos)
    {
        return (b & (1 << pos)) != 0;
    }
}