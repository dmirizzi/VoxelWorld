public static class GameBlockProperties
{
    public static DoorStateProperty DoorState(bool isTopPart, bool isOpen) => new(isTopPart, isOpen);
}
