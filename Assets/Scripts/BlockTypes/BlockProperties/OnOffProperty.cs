public class OnOffProperty
{
    // true is on, false is off
    [BitField(1)]
    public bool OnOffState { get; set; }

    public static OnOffProperty On = new OnOffProperty { OnOffState = true };

    public static OnOffProperty Off = new OnOffProperty { OnOffState = false };
}