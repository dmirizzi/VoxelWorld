using System.Collections.Generic;
using UnityEngine;

public static class TerrainFlattener
{
    public static void Flatten(
        ChunkUpdateBuilder builder,
        int centerX, int centerZ,
        int halfW, int halfD,
        TerrainHeightSampler heightSampler,
        ushort surfaceBlock,
        ushort subsurfaceBlock,
        int subsurfaceDepth)
    {
        var heights = new List<int>((2 * halfW + 1) * (2 * halfD + 1));
        for (int z = centerZ - halfD; z <= centerZ + halfD; z++)
        for (int x = centerX - halfW; x <= centerX + halfW; x++)
            heights.Add(heightSampler.GetHeight(x, z));

        heights.Sort();
        int targetY = heights[heights.Count / 2];

        for (int z = centerZ - halfD; z <= centerZ + halfD; z++)
        for (int x = centerX - halfW; x <= centerX + halfW; x++)
        {
            int sampledHeight = heightSampler.GetHeight(x, z);
            int cutTop        = Mathf.Max(sampledHeight, targetY + 4);

            // Carve air above targetY to cut any hills
            for (int y = targetY + 1; y <= cutTop; y++)
                builder.QueueGlobalVoxel(new Vector3Int(x, y, z), 0);

            builder.QueueGlobalVoxel(new Vector3Int(x, targetY, z), surfaceBlock);

            for (int i = 1; i <= subsurfaceDepth; i++)
                builder.QueueGlobalVoxel(new Vector3Int(x, targetY - i, z), subsurfaceBlock);
        }
    }
}
