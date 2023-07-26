using System.Collections.Generic;
using UnityEngine;

public static class BlockFaceHelper
{
    private static Dictionary<Vector3, BlockFace> _mapping = new Dictionary<Vector3, BlockFace>
    {
        { Vector3.up,       BlockFace.Top },
        { Vector3.down,     BlockFace.Bottom },
        { Vector3.back,     BlockFace.Front },
        { Vector3.forward,  BlockFace.Back },
        { Vector3.left,     BlockFace.Left },
        { Vector3.right,    BlockFace.Right }
    };

    private static Dictionary<BlockFace, Vector3> _reverseMapping = new Dictionary<BlockFace, Vector3>
    {
        { BlockFace.Top,       Vector3.up },
        { BlockFace.Bottom,    Vector3.down },
        { BlockFace.Front,     Vector3.back },
        { BlockFace.Back,      Vector3.forward },
        { BlockFace.Left,      Vector3.left },
        { BlockFace.Right,     Vector3.right }
    };

    private static Dictionary<Vector3Int, BlockFace> _mappingInt = new Dictionary<Vector3Int, BlockFace>
    {
        { Vector3Int.up,       BlockFace.Top },
        { Vector3Int.down,     BlockFace.Bottom },
        { Vector3Int.back,     BlockFace.Front },
        { Vector3Int.forward,  BlockFace.Back },
        { Vector3Int.left,     BlockFace.Left },
        { Vector3Int.right,    BlockFace.Right }
    };

    private static Dictionary<BlockFace, Vector3Int> _reverseMappingInt = new Dictionary<BlockFace, Vector3Int>
    {
        { BlockFace.Top,       Vector3Int.up },
        { BlockFace.Bottom,    Vector3Int.down },
        { BlockFace.Front,     Vector3Int.back },
        { BlockFace.Back,      Vector3Int.forward },
        { BlockFace.Left,      Vector3Int.left },
        { BlockFace.Right,     Vector3Int.right }
    };

    private static Dictionary<BlockFace, int> _blockFaceTurnNo = new Dictionary<BlockFace, int>()
    {
        { BlockFace.Front, 0},
        { BlockFace.Left, 1 },
        { BlockFace.Back, 2 },
        { BlockFace.Right, 3 },
    };

    private static List<BlockFace> _yRotationClockwiseLookup = new List<BlockFace> 
    {
        BlockFace.Front,
        BlockFace.Left,
        BlockFace.Back,
        BlockFace.Right
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

    public static Vector3 GetVectorRelativeToFace(BlockFace face, BlockFace dir)
    {
        return GetVectorFromBlockFace(RotateFaceY(face, dir));
    }

    public static BlockFace RotateFaceY(BlockFace face, BlockFace direction)
    {
        var angle = GetYAngleBetweenFaces(direction, BlockFace.Back);
        return RotateFaceY(face, angle);
    }

    public static BlockFace RotateFaceY(BlockFace face, int yAngleDeg)
    {
        if(face == BlockFace.Top || face == BlockFace.Bottom)
        {
            return face;
        }

        if(yAngleDeg < 0)
        {
            yAngleDeg = 360 - yAngleDeg;
        }

        var turn = _blockFaceTurnNo[face];
        var turnOffset = yAngleDeg / 90;
        return _yRotationClockwiseLookup[(turn + turnOffset) % 4];
    }

    public static int GetYAngleBetweenFaces(BlockFace from, BlockFace to)
    {
        if(from == BlockFace.Top || from == BlockFace.Bottom || to == BlockFace.Top || to == BlockFace.Bottom )
        {
            return 0;
        }

        var turns = _blockFaceTurnNo[to] - _blockFaceTurnNo[from];

        if(System.Math.Abs(turns) > 2)
        {
            // Wrap around for shortest rotation
            return -System.Math.Sign(turns) * 90;
        }
        if(turns == -2)
        {
            // Just to mimic Vector3.SignedAngle's behavior which was used in a previous implementation
            // of this method
            return turns * -90;
        }

        return turns * 90;
    }


    public static BlockFace? GetBlockFaceFromVector(Vector3 vector)
    {
        foreach(var vec in _mapping.Keys)
        {
            if(Mathf.Approximately(Vector3.Dot(vector, vec), 1f))
            {
                return _mapping[vec];
            }
        }
        return null;
    }

    public static Vector3 GetVectorFromBlockFace(BlockFace face)
    {
        return _reverseMapping[face];
    }

    public static Vector3Int GetVectorIntFromBlockFace(BlockFace face)
    {
        return _reverseMappingInt[face];
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