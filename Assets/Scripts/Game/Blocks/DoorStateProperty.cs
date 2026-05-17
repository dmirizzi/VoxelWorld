public class DoorStateProperty
{
    public DoorStateProperty() {}

    public DoorStateProperty(bool isTopPart, bool isOpen)
    {
        IsTopPart = isTopPart;
        IsOpen = isOpen;
    }

    [BitField(1)]
    public bool IsTopPart { get; set; }

    [BitField(2)]
    public bool IsOpen { get; set; }
}