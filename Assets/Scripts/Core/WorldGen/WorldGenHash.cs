public static class WorldGenHash
{
    public static int Pos(int seed, int x, int z)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x; h *= 0x9e3779b9u; h ^= h >> 16;
            h ^= (uint)z; h *= 0x85ebca6bu; h ^= h >> 13;
                           h *= 0xc2b2ae35u; h ^= h >> 16;
            return (int)h;
        }
    }

    public static int Str(string s)
    {
        unchecked
        {
            uint h = 0x9e3779b9u;
            foreach (char c in s)
            {
                h ^= (uint)c;
                h *= 0x9e3779b9u; h ^= h >> 16;
                h *= 0x85ebca6bu; h ^= h >> 13;
            }
            h *= 0xc2b2ae35u;
            h ^= h >> 16;
            return (int)h;
        }
    }
}
