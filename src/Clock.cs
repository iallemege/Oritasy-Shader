using HarmonyLib;

namespace OritasyShader;

/// <summary>
/// Two clocks. The mission clock (sun and time of day) is scaled down to 0.001 of the mission's
/// own speed and accumulated in double precision, because the game's float step disappears that
/// slowly. The event clock is Time.timeScale: 0.001 runs the match itself in slow motion.
/// </summary>
static class Clock
{
    const double Hour = 1.0 / 3600.0;
    static readonly FieldInfo Factor = AccessTools.Field(typeof(LevelInfo), "timeFactor");
    static float _base = -1f;
    static double _tod = double.NaN;
    static bool _drive;
    static float _event = 1f;

    public static void Snap(float hours)
    {
        _tod = hours < 0f ? 0f : hours >= 24f ? hours % 24f : hours;
    }

    public static void Tick()
    {
        float want = Mathf.Clamp(Opt.EventRate, 0.001f, 1f);
        if (Paused()) return;
        if (Mathf.Abs(want - _event) < 0.0000005f && Mathf.Abs(Time.timeScale - want) < 0.0000005f) return;
        if (want >= 0.999f)
        {
            if (Time.timeScale > 0.0001f && Mathf.Abs(Time.timeScale - 1f) > 0.0001f && Mathf.Abs(_event - 1f) > 0.0001f)
                Time.timeScale = 1f;
            _event = 1f;
            return;
        }
        Time.timeScale = want;
        _event = want;
    }

    static bool Paused()
    {
        try { return GameplayUI.GameIsPaused; }
        catch { return false; }
    }

    static bool Server()
    {
        try
        {
            NetworkManagerNuclearOption nm = NetworkManagerNuclearOption.i;
            return nm != null && nm.Server != null && nm.Server.Active;
        }
        catch { return false; }
    }

    public static void Before(LevelInfo li)
    {
        _drive = false;
        if (li == null || Factor == null || !Server() || Paused()) return;
        float cur = 0f;
        try { cur = (float)Factor.GetValue(li); }
        catch { return; }
        if (cur > 0.0001f) _base = cur;
        float rate = Mathf.Clamp(Opt.TimeRate, 0.001f, 16f);
        if (_base < 0f) return;
        bool clock = rate <= 0.999f || rate >= 1.001f;
        bool slow = Opt.EventRate < 0.999f;
        if (!clock && !slow)
        {
            _tod = li.timeOfDay;
            return;
        }
        if (!clock && slow)
            rate = 1f;
        float effective = _base * rate;
        // Slow motion scales Time.deltaTime, which would drag the sun along with the match.
        // Below a factor of 1 the game's own float add is smaller than the clock can store.
        if (!slow && effective >= 1f)
        {
            try { Factor.SetValue(li, effective); } catch { }
            _tod = li.timeOfDay;
            return;
        }
        try { Factor.SetValue(li, 0f); } catch { return; }
        _drive = true;
        Opt.TimeRate = Mathf.Clamp(Opt.TimeRate, 0.001f, 16f);
    }

    public static void After(LevelInfo li)
    {
        if (!_drive || li == null || _base < 0f) return;
        if (double.IsNaN(_tod)) _tod = li.timeOfDay;
        double rate = Mathf.Clamp(Opt.TimeRate, 0.001f, 16f);
        _tod += _base * rate * Time.unscaledDeltaTime * Hour;
        if (_tod >= 24.0) _tod -= 24.0;
        if (_tod < 0.0) _tod += 24.0;
        float next = (float)_tod;
        if (next != li.timeOfDay)
        {
            try { li.SetTimeOfDay(next); }
            catch { li.timeOfDay = next; }
        }
    }
}

[HarmonyPatch(typeof(LevelInfo), "Update")]
static class H_Clock
{
    static void Prefix(LevelInfo __instance) => Clock.Before(__instance);
    static void Postfix(LevelInfo __instance) => Clock.After(__instance);
}
