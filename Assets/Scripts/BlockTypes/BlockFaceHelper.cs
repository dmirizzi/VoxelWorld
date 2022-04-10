using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class BlockFaceHelper
{
    private static Dictionary<Vector3, BlockFace> _mapping = new Dictionary<Vector3, BlockFace>()
    {
        { Vector3.up,       BlockFace.Top },
        { Vector3.down,     BlockFace.Bottom },
        { Vector3.back,     BlockFace.Front },
        { Vector3.forward,  BlockFace.Back },
        { Vector3.left,     BlockFace.Left },
        { Vector3.right,    BlockFace.Right }
    };

    public static BlockFaceSelector ToBlockFaceSelector(BlockFace face)
    {
        switch(face)
        {
            case BlockFace.Top:     return BlockFaceSelector.Top;
            case BlockFace.Bottom:  return BlockFaceSelector.Bottom;
            case BlockFace.Front:   return BlockFaceSelector.Front;
            case BlockFace.Back:    return BlockFaceSelector.Back;
            case BlockFace.Left:    return BlockFaceSelector.Left;
            case BlockFace.Right:   return BlockFaceSelector.Right;

            default: throw new System.ArgumentException($"Unknown block face {face}");
        }
    }

    public static BlockFace? GetBlockFaceFromVector(Vector3 normal)
    {
        foreach(var vec in _mapping.Keys)
        {
            if(Mathf.Approximately(Vector3.Dot(normal, vec), 1f))
            {
                return _mapping[vec];
            }
        }
        return null;
    }

    public static Vector3 GetVectorFromBlockFace(BlockFace face)
    {
        return _mapping.Single(kv => kv.Value == face).Key;
    }

    public static BlockFace GetOppositeFace(BlockFace face)
    {
        int faceInt = (int)face;
        if(faceInt % 2 == 0)
        {
            faceInt++;
        }
        else
        {
            faceInt--;
        }
        return (BlockFace)faceInt;
    }
}