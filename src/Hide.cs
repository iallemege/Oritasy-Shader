namespace OritasyShader;

/// <summary>Hides the game's screen UI and, while that is on, OritasySS's own drawing.</summary>
public static class Hide
{
    const float Rescan = 0.5f;
    static readonly AccessTools.FieldRef<GameplayUI, Canvas> MenuCanvas = AccessTools.FieldRefAccess<GameplayUI, Canvas>("menuCanvas");
    static readonly System.Collections.Generic.List<(CanvasGroup g, bool ours, float alpha, bool rays)> _faded = new(32);
    static readonly System.Collections.Generic.HashSet<Canvas> _seen = new();
    static float _scanAt;

    public static bool Active { get; private set; }

    /// <summary>The player asked for a clear screen. OritasySS stops drawing while this is set.</summary>
    public static bool Cover => Opt.UiHidden && Opt.InMatch && !Paused() && !MapUp();

    public static void Tick()
    {
        if (!Opt.InMatch) Opt.UiHidden = false;
        else if (Input.GetKeyDown(Opt.UiKey)) Opt.UiHidden = !Opt.UiHidden;
        bool want = Cover;
        if (want != Active)
        {
            Active = want;
            if (!want) Restore();
            _scanAt = 0f;
        }
        if (!Active) return;
        for (int i = _faded.Count - 1; i >= 0; i--)
        {
            CanvasGroup g = _faded[i].g;
            if (g == null) _faded.RemoveAt(i);
            else if (g.alpha != 0f) g.alpha = 0f;
        }
        if (Time.unscaledTime < _scanAt) return;
        _scanAt = Time.unscaledTime + Rescan;
        foreach (Canvas c in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (c == null || !c.isRootCanvas || Keep(c) || !_seen.Add(c)) continue;
            bool ours = !c.TryGetComponent(out CanvasGroup g);
            if (ours) g = c.gameObject.AddComponent<CanvasGroup>();
            _faded.Add((g, ours, g.alpha, g.blocksRaycasts));
            g.alpha = 0f;
            g.blocksRaycasts = false;
        }
    }

    static bool Keep(Canvas c)
    {
        if (c.renderMode == RenderMode.WorldSpace) return true;
        try
        {
            if (c.GetComponentInParent<Aircraft>(true) != null) return true;
            if (c.GetComponentInParent<TacScreen>(true) != null) return true;
            if (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera != null && c.worldCamera.targetTexture != null)
                return true;
        }
        catch { }
        return false;
    }

    static bool Paused()
    {
        try
        {
            if (GameplayUI.GameIsPaused) return true;
            GameplayUI ui = SceneSingleton<GameplayUI>.i;
            Canvas menu = ui != null ? MenuCanvas(ui) : null;
            return menu != null && menu.enabled;
        }
        catch { return false; }
    }

    static bool MapUp()
    {
        try { return DynamicMap.mapMaximized; }
        catch { return false; }
    }

    static void Restore()
    {
        foreach (var f in _faded)
        {
            if (f.g == null) continue;
            if (f.ours) UnityEngine.Object.Destroy(f.g);
            else { f.g.alpha = f.alpha; f.g.blocksRaycasts = f.rays; }
        }
        _faded.Clear();
        _seen.Clear();
    }
}

[HarmonyPatch]
static class H_SsDraw
{
    static bool Prepare() => AccessTools.TypeByName("OritasySS.Plugin") != null;

    static MethodBase TargetMethod() =>
        AccessTools.Method(AccessTools.TypeByName("OritasySS.Plugin"), "OnGUI");

    static bool Prefix() => !Hide.Cover;
}
