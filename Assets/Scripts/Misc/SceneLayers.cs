using UnityEngine;

public static class SceneLayers
{
    public static int Voxels { get; } = LayerMask.NameToLayer("Voxels");

    public static void SetLayer(this GameObject gameObject, int layer)
    {
        if (null == gameObject)
        {
            return;
        }
       
        gameObject.layer = layer;
       
        foreach (Transform child in gameObject.transform)
        {
            if (child == null)
            {
                continue;
            }
            SetLayer(child.gameObject, layer);
        }
    }
}