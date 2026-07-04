namespace LightRaiders
{
    /// <summary>
    /// Substitutable intent provider for tests and automation: returns whatever
    /// intent was last written to <see cref="Intent"/>.
    /// </summary>
    public sealed class ScriptedRaiderIntentProvider : IRaiderIntentProvider
    {
        public RaiderIntent Intent;

        public RaiderIntent GetIntent() => Intent;
    }
}
