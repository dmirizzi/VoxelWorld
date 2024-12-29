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
            for(int y = 0; y < VoxelInfo.ChunkSize; ++y)
            {
                for(int x = 0; x < VoxelInfo.ChunkSize; ++x)
                {
                    var localVoxelPos = new Vector3Int(x, y, z);
                    var globalVoxelPos = chunkBasePos + localVoxelPos;
                    var terrainHeight = GetTerrainHeight(globalVoxelPos);

                    if(globalVoxelPos.y < terrainHeight)
                    {
                        builder.QueueVoxel(localVoxelPos, _dirtType);
                    }
                    else if(globalVoxelPos.y == terrainHeight)
                    {
                        builder.QueueVoxel(localVoxelPos, _grassType);
/*
                        if(TreeShouldBePlaced(localVoxelPos) && (x % 15 == 0 || y % 15 == 0 || z % 15 == 0))
                        {
                            PlaceTree(builder, localVoxelPos, 4, 3);
                        }
                        */
                    }
                    else if(globalVoxelPos.y == terrainHeight + 1)
                    {
                        if((globalVoxelPos.x % 30) == 0 
                        && (globalVoxelPos.z % 30) == 0 )
                        {
                            builder.QueueVoxel(localVoxelPos, _torchType);
                        }
                    }
                }
            }
        }

        return builder.GetChunkUpdate();
    }
    private int GetTerrainHeight(Vector3Int globalVoxelPos)
    {
        var height = 0;
        
        // Local terrain variations
        height += (int)(Mathf.PerlinNoise((float)globalVoxelPos.x / 40.0f, (float)globalVoxelPos.z / 40.0f) * 32) - 5;

        // Macro terrain
        height += (int)(Mathf.PerlinNoise((float)globalVoxelPos.x / 400.0f, (float)globalVoxelPos.z / 400.0f) * 512) - 256;

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
 