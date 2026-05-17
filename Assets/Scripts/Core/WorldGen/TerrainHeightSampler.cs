using UnityEngine;

public class TerrainHeightSampler
{
    public TerrainHeightSampler(int seed)
    {
        var rng = new System.Random(seed);
        _noiseOffsetX = (float)(rng.NextDouble() * 10000.0);
        _noiseOffsetZ = (float)(rng.NextDouble() * 10000.0);
    }

    public int GetHeight(int globalX, int globalZ)
    {
        float nx = globalX + _noiseOffsetX;
        float nz = globalZ + _noiseOffsetZ;

        float continental     = Mathf.PerlinNoise(nx / 600f, nz / 600f);
        float continentalBias = (continental * continental - 0.25f) * 55f;
        float roughness       = Mathf.PerlinNoise(nx / 280f + 100f, nz / 280f + 100f);

        float h = continentalBias;
        h += Mathf.PerlinNoise(nx / 250f, nz / 250f) * 60f;
        h += Mathf.PerlinNoise(nx /  80f, nz /  80f) * (12f + roughness * 24f);
        h += Mathf.PerlinNoise(nx /  30f, nz /  30f) *  8f;
        h += Mathf.PerlinNoise(nx /  10f, nz /  10f) *  2f;
        h -= 50f;
        return Mathf.RoundToInt(h);
    }

    private readonly float _noiseOffsetX;
    private readonly float _noiseOffsetZ;
}
