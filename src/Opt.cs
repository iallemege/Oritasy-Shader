namespace OritasyShader;

/// <summary>Settings for the shader menu. OritasySS copies its display-page edits here when both are loaded.</summary>
public static class Opt
{
    public static bool Zh = true;
    public static bool GradeOn = true, SharpOn = true, SkyOn = true, UiHidden;
    public static float GradeContrast = 44f, GradeSat = 32f, GradeTemp = 9f, GradeTint, GradeHue;
    public static float GradeLift, GradeGamma, GradeGain, GradeShadowTone, GradeHighTone, GradeSplitBalance;
    public static float GradeShadowHue = 210f, GradeHighHue = 35f;
    public static float GradeVignette, GradeGrain, GradeBloom, GradeBloomThreshold = 0.9f, GradeBloomScatter = 0.7f, GradeChroma;
    public static float GradeLutMix = 1f, SharpAmt = 0.95f;
    public static int GradeTonemap;
    public static string GradeLut = "";
    public static float LightSun = 1f, LightSky = 1f, LightFog = 1f, LightShadow, LightShadowKm;
    public static float CloudCover = 1f, CloudDensity = 1f, CloudLight = 1f, CloudDistKm = 60f, CloudSteps = 24f;
    public static KeyCode UiKey = KeyCode.F10, MenuKey = KeyCode.F2;
    /// <summary>1 keeps the mission clock. 0.001 is one thousandth of that speed.</summary>
    public static float TimeRate = 1f;
    /// <summary>1 is realtime. 0.001 is slow motion of the match itself.</summary>
    public static float EventRate = 1f;
    public static int Wx;
    public static float Weather = -1f, CloudMeters = -1f;
    public static float WinX = -1f, WinY = -1f, WinW = 560f, WinH = 680f;

    public static string Root => Path.Combine(Paths.ConfigPath, "OritasyShader");
    public static bool InMatch
    {
        get
        {
            try
            {
                GameState gs = GameManager.gameState;
                return gs == GameState.SinglePlayer || gs == GameState.Multiplayer;
            }
            catch { return false; }
        }
    }
    public static Aircraft Plane()
    {
        try { return GameManager.GetLocalAircraft(out Aircraft a) ? a : null; }
        catch { return null; }
    }
    public static bool Taken(string guid)
    {
        try { return BepInEx.Bootstrap.Chainloader.PluginInfos != null && BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid); }
        catch { return false; }
    }
    public static string T(string en, string zh) => en;

    static ConfigFile _file;

    public static void Bind(ConfigFile c)
    {
        _file = c;
        Zh = c.Bind("Locale", "Chinese", true, "Menu language.").Value;
        GradeOn = c.Bind("Theater", "Grade", true, "").Value;
        SharpOn = c.Bind("Theater", "Sharpen", true, "").Value;
        GradeContrast = c.Bind("Theater", "GradeContrast", 44f, "").Value;
        GradeSat = c.Bind("Theater", "GradeSaturation", 32f, "").Value;
        GradeTemp = c.Bind("Theater", "GradeTemperature", 9f, "").Value;
        GradeTint = c.Bind("Theater", "GradeTint", 0f, "").Value;
        GradeHue = c.Bind("Theater", "GradeHueShift", 0f, "").Value;
        GradeLift = c.Bind("Theater", "GradeShadows", 0f, "").Value;
        GradeGamma = c.Bind("Theater", "GradeMidtones", 0f, "").Value;
        GradeGain = c.Bind("Theater", "GradeHighlights", 0f, "").Value;
        GradeTonemap = c.Bind("Theater", "GradeToneMapping", 0, "").Value;
        GradeShadowTone = c.Bind("Theater", "GradeShadowTint", 0f, "").Value;
        GradeShadowHue = c.Bind("Theater", "GradeShadowHue", 210f, "").Value;
        GradeHighTone = c.Bind("Theater", "GradeHighlightTint", 0f, "").Value;
        GradeHighHue = c.Bind("Theater", "GradeHighlightHue", 35f, "").Value;
        GradeSplitBalance = c.Bind("Theater", "GradeSplitBalance", 0f, "").Value;
        GradeVignette = c.Bind("Theater", "GradeVignette", 0f, "").Value;
        GradeGrain = c.Bind("Theater", "GradeFilmGrain", 0f, "").Value;
        GradeBloom = c.Bind("Theater", "GradeBloom", 0f, "").Value;
        GradeBloomThreshold = c.Bind("Theater", "GradeBloomThreshold", 0.9f, "").Value;
        GradeBloomScatter = c.Bind("Theater", "GradeBloomScatter", 0.7f, "").Value;
        GradeChroma = c.Bind("Theater", "GradeChromaticAberration", 0f, "").Value;
        SharpAmt = Mathf.Clamp01(c.Bind("Theater", "SharpenStrength", 0.95f, "").Value);
        GradeLut = c.Bind("Theater", "GradeLut", "", "").Value ?? "";
        GradeLutMix = Mathf.Clamp01(c.Bind("Theater", "GradeLutStrength", 1f, "").Value);
        LightSun = c.Bind("Theater", "LightSun", 1f, "").Value;
        LightSky = c.Bind("Theater", "LightSky", 1f, "").Value;
        LightFog = c.Bind("Theater", "LightFog", 1f, "").Value;
        LightShadow = c.Bind("Theater", "LightShadowStrength", 0f, "").Value;
        LightShadowKm = c.Bind("Theater", "LightShadowDistanceKm", 0f, "").Value;
        SkyOn = c.Bind("Weather", "Sky", true, "").Value;
        CloudCover = c.Bind("Weather", "CloudCover", 1f, "").Value;
        CloudDensity = c.Bind("Weather", "CloudDensity", 1f, "").Value;
        CloudLight = c.Bind("Weather", "CloudBrightness", 1f, "").Value;
        CloudDistKm = c.Bind("Weather", "CloudDistanceKm", 60f, "").Value;
        CloudSteps = c.Bind("Weather", "CloudSteps", 24f, "").Value;
        Wx = c.Bind("Weather", "Regime", 0, "0 fair, 1 breezy, 2 front, 3 gale, 4 storm, 5 fog.").Value;
        Weather = c.Bind("Weather", "Intensity", -1f, "Below 0 leaves the mission weather.").Value;
        CloudMeters = c.Bind("Weather", "CloudHeight", -1f, "Below 0 leaves the mission cloud height.").Value;
        TimeRate = Mathf.Clamp(c.Bind("Time", "ClockRate", 1f, "Mission clock speed. 0.001 to 16. 1 is the mission's own.").Value, 0.001f, 16f);
        EventRate = Mathf.Clamp(c.Bind("Time", "EventRate", 1f, "Slow motion of the match. 0.001 to 1.").Value, 0.001f, 1f);
        UiHidden = c.Bind("Presentation", "HideUi", false, "").Value;
        UiKey = c.Bind("Presentation", "HideUiKey", KeyCode.F10, "").Value;
        MenuKey = c.Bind("Presentation", "MenuKey", KeyCode.F2, "").Value;
        WinX = c.Bind("Window", "X", -1f, "").Value;
        WinY = c.Bind("Window", "Y", -1f, "").Value;
        WinW = c.Bind("Window", "W", 560f, "").Value;
        WinH = c.Bind("Window", "H", 680f, "").Value;
    }

    public static void Save(string sec, string key, object v)
    {
        if (_file == null || v == null) return;
        try
        {
            if (v is bool b) { var e = _file.Bind(sec, key, b, ""); if (e.Value != b) e.Value = b; }
            else if (v is int i) { var e = _file.Bind(sec, key, i, ""); if (e.Value != i) e.Value = i; }
            else if (v is float f) { var e = _file.Bind(sec, key, f, ""); if (Mathf.Abs(e.Value - f) > 0.00001f) e.Value = f; }
            else if (v is string s) { var e = _file.Bind(sec, key, s, ""); if (e.Value != s) e.Value = s; }
            else if (v is KeyCode k) { var e = _file.Bind(sec, key, k, ""); if (e.Value != k) e.Value = k; }
        }
        catch { }
    }

    public static void SaveWindow()
    {
        Save("Window", "X", WinX);
        Save("Window", "Y", WinY);
        Save("Window", "W", WinW);
        Save("Window", "H", WinH);
    }

    public static void Set(string key, object value)
    {
        if (key == null || value == null) return;
        float F() => value is float f ? f : value is int i ? i : value is double d ? (float)d : 0f;
        bool B() => value is bool b && b;
        switch (key)
        {
            case "Grade": GradeOn = B(); break;
            case "GradeContrast": GradeContrast = F(); break;
            case "GradeSaturation": GradeSat = F(); break;
            case "GradeTemperature": GradeTemp = F(); break;
            case "GradeTint": GradeTint = F(); break;
            case "GradeHueShift": GradeHue = F(); break;
            case "GradeShadows": GradeLift = F(); break;
            case "GradeMidtones": GradeGamma = F(); break;
            case "GradeHighlights": GradeGain = F(); break;
            case "GradeToneMapping": GradeTonemap = value is int n ? n : (int)F(); break;
            case "GradeShadowTint": GradeShadowTone = F(); break;
            case "GradeShadowHue": GradeShadowHue = F(); break;
            case "GradeHighlightTint": GradeHighTone = F(); break;
            case "GradeHighlightHue": GradeHighHue = F(); break;
            case "GradeSplitBalance": GradeSplitBalance = F(); break;
            case "GradeVignette": GradeVignette = F(); break;
            case "GradeFilmGrain": GradeGrain = F(); break;
            case "GradeBloom": GradeBloom = F(); break;
            case "GradeBloomThreshold": GradeBloomThreshold = F(); break;
            case "GradeBloomScatter": GradeBloomScatter = F(); break;
            case "GradeChromaticAberration": GradeChroma = F(); break;
            case "Sharpen": SharpOn = B(); break;
            case "SharpenStrength": SharpAmt = F(); break;
            case "GradeLut": GradeLut = value as string ?? ""; break;
            case "LightSun": LightSun = F(); break;
            case "LightSky": LightSky = F(); break;
            case "LightFog": LightFog = F(); break;
            case "LightShadowStrength": LightShadow = F(); break;
            case "LightShadowDistanceKm": LightShadowKm = F(); break;
            case "Sky": SkyOn = B(); break;
            case "CloudCover": CloudCover = F(); break;
            case "CloudDensity": CloudDensity = F(); break;
            case "CloudBrightness": CloudLight = F(); break;
            case "CloudDistanceKm": CloudDistKm = F(); break;
            case "CloudSteps": CloudSteps = F(); break;
            case "UiHidden": UiHidden = B(); break;
            case "HideUiKey": if (value is KeyCode k) UiKey = k; break;
        }
    }
}
