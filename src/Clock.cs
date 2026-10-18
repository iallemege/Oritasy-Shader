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
    static float _mission = -1f;
    static bool _latched;
    static double _tod = double.NaN;
    static bool _drive;
    static float _event = 1f;

    public static void Snap(float hours)
    {
        if (float.IsNaN(hours)) hours = 12f;
        hours %= 24f;
        if (hours < 0f) hours += 24f;
        _tod = hours;
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
        // The mission value is latched once. Writing the sped-up factor back and
        // reading it next frame multiplies the speed again until the day number
        // leaves the lighting curves and the world goes black.
        if (!_latched)
        {
            if (cur > 0.0001f && cur <= 120f) { _mission = cur; _latched = true; }
            else if (cur > 120f) { _mission = 1f; _latched = true; }
            else return;
        }
        Wrap(li);
        float rate = Mathf.Clamp(Opt.TimeRate, 0.001f, 16f);
        bool clock = rate <= 0.999f || rate >= 1.001f;
        bool slow = Opt.EventRate < 0.999f;
        if (!clock && !slow)
        {
            try { Factor.SetValue(li, _mission); } catch { }
            _tod = li.timeOfDay;
            return;
        }
        if (!clock && slow) rate = 1f;
        float effective = _mission * rate;
        if (!slow && effective >= 1f)
        {
            try { Factor.SetValue(li, effective); } catch { }
            _tod = li.timeOfDay;
            return;
        }
        try { Factor.SetValue(li, 0f); } catch { return; }
        _drive = true;
    }

    public static void After(LevelInfo li)
    {
        if (!_drive || li == null || _mission < 0f) return;
        if (double.IsNaN(_tod) || double.IsInfinity(_tod)) _tod = WrapHours(li.timeOfDay);
        double rate = Mathf.Clamp(Opt.TimeRate, 0.001f, 16f);
        _tod += _mission * rate * Time.unscaledDeltaTime * Hour;
        _tod = _tod % 24.0;
        if (_tod < 0.0) _tod += 24.0;
        float next = (float)_tod;
        if (float.IsNaN(next)) return;
        if (Mathf.Abs(Mathf.DeltaAngle(li.timeOfDay * 15f, next * 15f)) > 0.01f)
        {
            try { li.SetTimeOfDay(next); }
            catch { li.timeOfDay = next; }
        }
    }

    static void Wrap(LevelInfo li)
    {
        float t = li.timeOfDay;
        if (!float.IsNaN(t) && t >= 0f && t < 24f) return;
        float next = WrapHours(t);
        _tod = next;
        try { li.SetTimeOfDay(next); }
        catch { li.timeOfDay = next; }
    }

    static float WrapHours(float t)
    {
        if (float.IsNaN(t) || float.IsInfinity(t)) return 12f;
        t %= 24f;
        if (t < 0f) t += 24f;
        return t;
    }
}

[HarmonyPatch(typeof(LevelInfo), "Update")]
static class H_Clock
{
    static void Prefix(LevelInfo __instance) => Clock.Before(__instance);
    static void Postfix(LevelInfo __instance) => Clock.After(__instance);
}
