public class DoorStateProperty
{
    // Whether this block is the top or bottom part of the door
    [BitField(1)]
    public bool IsTopPart { get; set; }

    [BitField(2)]
    public bool IsOpen { get; set; }
}