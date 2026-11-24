namespace GHelper.USB
{
    // Mouse lighting needs only these enums. Kept here so the lightweight
    // mouse build does not have to pull in the full laptop Aura engine.
    public enum AuraMode : int
    {
        AuraStatic = 0,
        AuraBreathe = 1,
        AuraColorCycle = 2,
        AuraRainbow = 3,
        Star = 4,
        Rain = 5,
        Highlight = 6,
        Laser = 7,
        Ripple = 8,
        AuraStrobe = 10,
        Comet = 11,
        Flash = 12,
        HEATMAP = 20,
        GPUMODE = 21,
        AMBIENT = 22,
        BATTERY = 23,
        GRADIENT = 24,
        ZONETEST = 25,
        AUDIO = 26,
        AUDIOPULSE = 27,
        CUSTOM = 28,
    }

    public enum AuraSpeed : int
    {
        Slow = 0,
        Normal = 1,
        Fast = 2,
    }
}
