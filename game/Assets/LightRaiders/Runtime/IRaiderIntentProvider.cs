namespace LightRaiders
{
    /// <summary>
    /// Source of the local Raider's intent for the current tick. Implemented by
    /// hardware input (gameplay) and scripted providers (tests/automation).
    /// </summary>
    public interface IRaiderIntentProvider
    {
        RaiderIntent GetIntent();
    }
}
