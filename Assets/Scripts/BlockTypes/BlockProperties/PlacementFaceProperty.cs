public class PlacementFaceProperty
{
    [BitField(1, 3)]
    public BlockFace PlacementFace { get; set; }

    public PlacementFaceProperty()
    {        
    }

    public PlacementFaceProperty(BlockFace placementFace)
    {
        PlacementFace = placementFace;
    }
}