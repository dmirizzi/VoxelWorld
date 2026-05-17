public static class BlockProperties
{
    public static PlacementFaceProperty PlacementFace(BlockFace face) => new(face);
    public static OnOffProperty OnOff(bool on) => on ? OnOffProperty.On : OnOffProperty.Off;
}
