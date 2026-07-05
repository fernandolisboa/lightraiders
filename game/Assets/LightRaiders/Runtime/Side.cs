namespace LightRaiders
{
    /// <summary>
    /// Which side a combatant fights on. Damage only ever crosses sides
    /// (friendly fire off, ADR-0003 deferral): a projectile harms Health of the
    /// OPPOSITE side only. Raider fire is Side.Raider and damages Side.Hostile
    /// targets, never the shooter or other Raiders; #17's hostile emitter will
    /// fire Side.Hostile shots that damage Raiders.
    /// </summary>
    public enum Side
    {
        Raider,
        Hostile,
    }
}
