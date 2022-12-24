using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

public class ChunkSerializer
{
    public string SerializeChunk(Chunk chunk)
    {
        throw new NotImplementedException();
    }

    private string SerializeVoxelData(ReadOnly3DArray<ushort> voxelData)
    {
        return Compress( gzip => {  
        });
    }
    
    private string SerializeAuxiliaryData(IReadOnlyDictionary<Vector3Int, ushort> auxData)
    {
        return Compress( gzip => {  
        });
    }


    public static string Compress(Action<GZipStream> compressAction)
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(memoryStream, System.IO.Compression.CompressionLevel.Optimal))
            {
                compressAction(gzipStream);
            }
            return memoryStream.ToString();
        }
    }
    

}