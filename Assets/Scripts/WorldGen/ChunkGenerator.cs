using UnityEngine;

public class ChunkGenerator
{
    public ChunkGenerator()
    {
        _dirtType = BlockDataRepository.GetBlockTypeId("Dirt");
        _grassType = BlockDataRepository.GetBlockTypeId("Grass");
        _torchType = BlockDataRepository.GetBlockTypeId("Torch");
        _logType = BlockDataRepository.GetBlockTypeId("Log");
        _leavesType = BlockDataRepository.GetBlockTypeId("Leaves");
    }

    public ChunkUpdate GenerateChunk(Vector3Int chunkPos)
    {
        var builder = new ChunkUpdateBuilder(chunkPos);

        var chunkBasePos = VoxelPosHelper.ChunkPosToGlobalChunkBaseVoxelPos(chunkPos);

        for(int z = 0; z < VoxelInfo.ChunkSize; ++z)
        {
            for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
            {
                int globalX = chunkBasePos.x + x;
                int globalZ = chunkBasePos.z + z;
                int terrainHeight = GetTerrainHeight(globalX, globalZ);

                for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
                {
                    int globalY = chunkBasePos.y + y;

                    if(globalY < terrainHeight)
                    {
                        builder.QueueVoxelInChunk(x, y, z, _dirtType);
                    }
                    else if(globalY == terrainHeight)
                    {
                        builder.QueueVoxelInChunk(x, y, z, _grassType);

                        /*
                        if(TreeShouldBePlaced(new Vector3Int(x, y, z)) && (x % 15 == 0 || y % 15 == 0 || z % 15 == 0))
                        {
                            PlaceTree(builder, new Vector3Int(x, y, z), 4, 3);
                        }
                        */
                    }
                    else if(globalY == terrainHeight + 1)
                    {
                        if((globalX % 30) == 0 && (globalZ % 30) == 0)
                        {
                            builder.QueueVoxelInChunk(x, y, z, _torchType);
                        }
                    }
                }
            }
        }

        return builder.GetChunkUpdate();
    }
    private int GetTerrainHeight(int globalX, int globalZ)
    {
        var height = 0;

        // Local terrain variations
        height += (int)(Mathf.PerlinNoise(globalX / 40.0f, globalZ / 40.0f) * 32) - 5;

        // Macro terrain
        height += (int)(Mathf.PerlinNoise(globalX / 400.0f, globalZ / 400.0f) * 512) - 256;

        return height;
    }

    private void PlaceTree(
        ChunkUpdateBuilder builder, 
        Vector3Int localRootPos, 
        int trunkHeight, 
        int crownRadius)
    {
        for(int ty = 1; ty <= trunkHeight; ++ty)
        {
            builder.QueueVoxel(localRootPos + Vector3Int.up * ty, _logType);
        }

        for(int tz = -crownRadius; tz <= crownRadius; ++tz)
        {
            for(int ty = -crownRadius; ty <= crownRadius; ++ty)
            {
                for(int tx = -crownRadius; tx <= crownRadius; ++tx)
                {
                    builder.QueueVoxel(localRootPos + Vector3Int.up * trunkHeight + new Vector3Int(tx, ty + trunkHeight, tz), _leavesType);
                }
            }
        }
    }

    private bool TreeShouldBePlaced(Vector3Int globalVoxelPos)
    {
        //var rand = new System.Random(globalVoxelPos.x + 1000 * globalVoxelPos.y + 1000000 + globalVoxelPos.z);
        var rand = new System.Random();
        return rand.NextDouble() <= 0.001f;
    }

    private ushort _dirtType;

    private ushort _grassType;

    private ushort _torchType;

    private ushort _logType;
    
    private ushort _leavesType;    
}
 