namespace OritasyShader;

/// <summary>F2 window. Drag the title bar. Drag the corner to resize. The layout stays the dark Oritasy panel.</summary>
static class Menu
{
    static readonly string[] En = { "Time", "Sky", "Grade", "UI" };
    static readonly string[] Zh = { "时间", "天空", "光影", "界面" };
    static bool _open;
    static int _page, _mode, _font = -1;
    static float _todHold = -1f;
    static Vector2 _grab, _scroll;
    static Rect _win;
    static bool _placed, _dirty;
    static GUIStyle _box, _title, _lab, _small, _btn, _on, _tab;

    public static bool Open => _open;

    public static void Tick()
    {
        if (!Opt.InMatch) { _open = false; return; }
        if (Input.GetKeyDown(KeyCode.Escape) && _open) { _open = false; return; }
        if (Input.GetKeyDown(Opt.MenuKey)) _open = !_open;
    }

    public static void Draw()
    {
        if (!_open) return;
        Place();
        Event e = Event.current;
        Rect title = new(_win.x, _win.y, _win.width - 86f, 28f);
        Rect grip = new(_win.xMax - 18f, _win.yMax - 18f, 18f, 18f);
        if (e != null && e.button == 0)
        {
            if (e.type == EventType.MouseDown)
            {
                if (grip.Contains(e.mousePosition)) { _mode = 2; _grab = e.mousePosition; e.Use(); }
                else if (title.Contains(e.mousePosition)) { _mode = 1; _grab = e.mousePosition - new Vector2(_win.x, _win.y); e.Use(); }
            }
            else if (e.type == EventType.MouseDrag && _mode == 1)
            {
                _win.x = e.mousePosition.x - _grab.x;
                _win.y = e.mousePosition.y - _grab.y;
                Clamp(); _dirty = true; e.Use();
            }
            else if (e.type == EventType.MouseDrag && _mode == 2)
            {
                _win.width = Mathf.Max(400f, e.mousePosition.x - _win.x);
                _win.height = Mathf.Max(440f, e.mousePosition.y - _win.y);
                Clamp(); _dirty = true; e.Use();
            }
            else if (e.type == EventType.MouseUp && _mode != 0)
            {
                _mode = 0;
                if (_dirty) { Opt.WinX = _win.x; Opt.WinY = _win.y; Opt.WinW = _win.width; Opt.WinH = _win.height; Opt.SaveWindow(); _dirty = false; }
            }
        }
        Styles();
        GUI.Box(_win, GUIContent.none, _box);
        GUI.DrawTexture(new Rect(_win.x, _win.y, _win.width, 3f), Tex(new Color(0.45f, 0.85f, 1f, 0.95f)));
        GUI.Label(new Rect(_win.x + 12f, _win.y + 6f, _win.width - 100f, 22f), "<b>ORITASY</b>   " + En[_page], _title);
        if (GUI.Button(new Rect(_win.xMax - 78f, _win.y + 6f, 66f, 22f), "Close", _btn)) _open = false;
        float x = _win.x + 10f, y = _win.y + 34f, w = _win.width - 20f;
        float cw = (w - 9f) / 4f;
        for (int i = 0; i < 4; i++)
            if (GUI.Button(new Rect(x + i * (cw + 3f), y, cw, 24f), En[i], i == _page ? _on : _tab))
            { _page = i; _scroll = Vector2.zero; }
        y += 32f;
        GUI.Label(new Rect(_win.xMax - 16f, _win.yMax - 16f, 14f, 14f), "◢", _small);
        GUILayout.BeginArea(new Rect(x, y, w, _win.yMax - y - 16f));
        _scroll = GUILayout.BeginScrollView(_scroll);
        try { Body(); }
        catch (Exception ex) { Debug.LogWarning("[Oritasy Shader] " + ex.Message); }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    static void Body()
    {
        if (_page == 0) TimePage();
        else if (_page == 1) SkyPage();
        else if (_page == 2) GradePage();
        else UiPage();
    }

    static void TimePage()
    {
        Head(Opt.T("Time of day", "时刻"));
        LevelInfo li = null;
        try { li = NetworkSceneSingleton<LevelInfo>.i; } catch { }
        float live = li != null ? li.timeOfDay : 12f;
        if (float.IsNaN(live) || live < 0f || live >= 24f) live = 12f;
        bool hold = Input.GetMouseButton(0);
        float shown = hold && _todHold >= 0f ? _todHold : live;
        float tod = Slider("Hour", shown, 0f, 24f, "0.00");
        if (hold && Mathf.Abs(tod - shown) > 0.01f)
        {
            _todHold = Mathf.Repeat(tod, 24f);
            Clock.Snap(_todHold);
            if (li != null)
            {
                try { li.SetTimeOfDay(_todHold); }
                catch { li.timeOfDay = _todHold; }
            }
        }
        else if (!hold) _todHold = -1f;
        Head(Opt.T("Clock speed", "时间流速"));
        Note(Opt.T("How fast the sun and the clock move, relative to this mission. 0.001 is one thousandth.",
            "太阳和时钟相对本任务原来的速度。0.001 是千分之一。"));
        float rate = Log("Clock", Opt.TimeRate, 0.001f, 16f);
        if (Input.GetMouseButton(0) && Mathf.Abs(rate - Opt.TimeRate) > 0.0008f)
        { Opt.TimeRate = rate; Opt.Save("Time", "ClockRate", rate); }
        Head(Opt.T("Slow motion", "慢动作"));
        Note(Opt.T("Event speed of the match itself, separate from the clock. 0.001 is one thousandth. The host slows the whole match; anyone else slows only this machine.",
            "整局事件的速度，和时钟分开。最低 0.001 倍。主机放慢会带动整局，其他人只放慢自己这台。"));
        float ev = Log("Events", Opt.EventRate, 0.001f, 1f);
        if (Input.GetMouseButton(0) && Mathf.Abs(ev - Opt.EventRate) > 0.0008f)
        { Opt.EventRate = ev; Opt.Save("Time", "EventRate", ev); }
        Head(Opt.T("Weather", "气象"));
        string[] name = { "Fair", "Breezy", "Front", "Gale", "Storm", "Fog" };
        int hit = -1;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < 3; i++)
            if (GUILayout.Button(name[i], i == Opt.Wx ? _on : _tab, GUILayout.Height(24f))) hit = i;
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        for (int i = 3; i < 6; i++)
            if (GUILayout.Button(name[i], i == Opt.Wx ? _on : _tab, GUILayout.Height(24f))) hit = i;
        GUILayout.EndHorizontal();
        if (hit >= 0 && hit != Opt.Wx)
        {
            Opt.Wx = hit;
            Opt.Save("Weather", "Regime", hit);
            float[] preset = { 0.05f, 0.2f, 0.55f, 0.7f, 0.95f, 0.85f };
            Opt.Weather = preset[hit];
            Opt.Save("Weather", "Intensity", Opt.Weather);
        }
        float shownW = Opt.Weather < 0f && li != null ? li.conditions : Mathf.Clamp01(Opt.Weather < 0f ? 0f : Opt.Weather);
        float w = Slider(Opt.T("Intensity", "强度"), shownW, 0f, 1f, "0.00");
        if (Mathf.Abs(w - shownW) > 0.02f) { Opt.Weather = w; Opt.Save("Weather", "Intensity", w); }
        float shownH = Opt.CloudMeters < 0f && li != null ? li.cloudHeight : (Opt.CloudMeters < 0f ? 1800f : Opt.CloudMeters);
        float h = Slider(Opt.T("Cloud base m", "云底高度 m"), shownH, 100f, 12000f, "0");
        if (Mathf.Abs(h - shownH) > 30f) { Opt.CloudMeters = Mathf.Round(h / 10f) * 10f; Opt.Save("Weather", "CloudHeight", Opt.CloudMeters); }
        Note(Opt.T("Drag the title to move this window. Drag the corner to resize it.",
            "拖标题栏移动窗口。拖右下角改变大小。"));
    }

    static void SkyPage()
    {
        Head(Opt.T("Sky and clouds", "天空与云层"));
        Toggle(Opt.T("Volumetric sky", "体积云天空"), ref Opt.SkyOn, "Weather", "Sky");
        Note(Sky.Running
            ? Opt.T("Sky shaders running.", "天空着色器运行中。")
            : Opt.T("Sky shaders are not running (fx.bundle missing or unsupported).", "天空着色器未运行（缺少 fx.bundle 或不受支持）。"));
        if (!Opt.SkyOn) return;
        Fine(Opt.T("Cloud cover", "云量"), ref Opt.CloudCover, "Weather", "CloudCover", 0f, 2f);
        Fine(Opt.T("Thickness", "云层厚度"), ref Opt.CloudDensity, "Weather", "CloudDensity", 0f, 2f);
        Fine(Opt.T("Brightness", "云层亮度"), ref Opt.CloudLight, "Weather", "CloudBrightness", 0.3f, 2.5f);
        Fine(Opt.T("Distance km", "绘制距离 km"), ref Opt.CloudDistKm, "Weather", "CloudDistanceKm", 10f, 100f);
        Fine(Opt.T("Quality", "采样质量"), ref Opt.CloudSteps, "Weather", "CloudSteps", 8f, 48f);
    }

    static void GradePage()
    {
        Head(Opt.T("Scene light", "场景光照"));
        Fine(Opt.T("Sun ×", "阳光 ×"), ref Opt.LightSun, "Theater", "LightSun", 0.3f, 2f);
        Fine(Opt.T("Sky light ×", "天空光 ×"), ref Opt.LightSky, "Theater", "LightSky", 0.3f, 2f);
        Fine(Opt.T("Fog ×", "雾 ×"), ref Opt.LightFog, "Theater", "LightFog", 0f, 3f);
        Fine(Opt.T("Shadow dark", "阴影浓度"), ref Opt.LightShadow, "Theater", "LightShadowStrength", 0f, 1f);
        Fine(Opt.T("Shadow km", "阴影距离 km"), ref Opt.LightShadowKm, "Theater", "LightShadowDistanceKm", 0f, 8f);
        Head(Opt.T("Colour grade", "色彩分级"));
        Toggle(Opt.T("Grade", "色彩分级"), ref Opt.GradeOn, "Theater", "Grade");
        if (!Opt.GradeOn) return;
        Step(Opt.T("Contrast", "对比度"), ref Opt.GradeContrast, "Theater", "GradeContrast", -50f, 50f);
        Step(Opt.T("Saturation", "饱和度"), ref Opt.GradeSat, "Theater", "GradeSaturation", -50f, 50f);
        Step(Opt.T("Warm / cool", "冷暖"), ref Opt.GradeTemp, "Theater", "GradeTemperature", -50f, 50f);
        Step(Opt.T("Tint", "色调"), ref Opt.GradeTint, "Theater", "GradeTint", -50f, 50f);
        Step(Opt.T("Hue", "色相"), ref Opt.GradeHue, "Theater", "GradeHueShift", -60f, 60f);
        Step(Opt.T("Shadows", "阴影亮度"), ref Opt.GradeLift, "Theater", "GradeShadows", -60f, 60f);
        Step(Opt.T("Midtones", "中间调"), ref Opt.GradeGamma, "Theater", "GradeMidtones", -60f, 60f);
        Step(Opt.T("Highlights", "高光亮度"), ref Opt.GradeGain, "Theater", "GradeHighlights", -60f, 60f);
        int tm = Pick(Opt.GradeTonemap, Opt.T("Game map", "原版映射"), "Neutral", "ACES");
        if (tm >= 0) { Opt.GradeTonemap = tm; Opt.Save("Theater", "GradeToneMapping", tm); }
        Fine(Opt.T("Vignette", "暗角"), ref Opt.GradeVignette, "Theater", "GradeVignette", 0f, 0.6f);
        Fine(Opt.T("Grain", "胶片颗粒"), ref Opt.GradeGrain, "Theater", "GradeFilmGrain", 0f, 1f);
        Fine(Opt.T("Bloom", "泛光"), ref Opt.GradeBloom, "Theater", "GradeBloom", 0f, 5f);
        Fine(Opt.T("Fringes", "色差"), ref Opt.GradeChroma, "Theater", "GradeChromaticAberration", 0f, 1f);
        Toggle(Opt.T("Sharpen", "锐化"), ref Opt.SharpOn, "Theater", "Sharpen");
        if (Opt.SharpOn) Fine(Opt.T("Strength", "锐化强度"), ref Opt.SharpAmt, "Theater", "SharpenStrength", 0f, 1f);
    }

    static void UiPage()
    {
        Head(Opt.T("Hide the interface", "隐藏界面"));
        Toggle(Opt.T("Hide all UI", "隐藏全部界面"), ref Opt.UiHidden, "Presentation", "HideUi");
        Note(Opt.T("Also ", "飞行中按 ") + Opt.UiKey + Opt.T(" in flight. This window, the pause menu and the map stay visible.",
            " 也能切换。本窗口、暂停菜单和 M 地图保持显示。"));
    }

    static void Place()
    {
        if (_placed) return;
        _placed = true;
        float w = Mathf.Clamp(Opt.WinW, 400f, Screen.width - 20f);
        float h = Mathf.Clamp(Opt.WinH, 440f, Screen.height - 20f);
        float x = Opt.WinX < 0f ? (Screen.width - w) * 0.5f : Opt.WinX;
        float y = Opt.WinY < 0f ? (Screen.height - h) * 0.5f : Opt.WinY;
        _win = new Rect(x, y, w, h);
        Clamp();
    }

    static void Clamp()
    {
        _win.width = Mathf.Clamp(_win.width, 400f, Mathf.Max(400f, Screen.width - 8f));
        _win.height = Mathf.Clamp(_win.height, 440f, Mathf.Max(440f, Screen.height - 8f));
        _win.x = Mathf.Clamp(_win.x, 0f, Mathf.Max(0f, Screen.width - _win.width));
        _win.y = Mathf.Clamp(_win.y, 0f, Mathf.Max(0f, Screen.height - _win.height));
    }

    static void Styles()
    {
        int fs = Mathf.Clamp(Mathf.RoundToInt(13f * _win.height / 680f), 11, 22);
        if (_box != null && _font == fs) return;
        _font = fs;
        Color text = new(0.86f, 0.93f, 0.96f);
        _box = Make(fs, text);
        _box.normal.background = Tex(new Color(0.03f, 0.06f, 0.08f, 0.92f));
        _title = Make(fs + 2, new Color(0.45f, 0.85f, 1f));
        _title.fontStyle = FontStyle.Bold;
        _lab = Make(fs, text);
        _lab.wordWrap = true;
        _small = Make(Mathf.Max(10, fs - 2), text);
        _small.wordWrap = true;
        _btn = Make(fs, text);
        _btn.alignment = TextAnchor.MiddleCenter;
        _btn.normal.background = Tex(new Color(0.12f, 0.2f, 0.25f, 0.9f));
        _btn.hover.background = Tex(new Color(0.18f, 0.32f, 0.4f, 0.95f));
        _tab = new GUIStyle(_btn);
        _tab.normal.background = Tex(new Color(0.08f, 0.13f, 0.16f, 0.9f));
        _on = new GUIStyle(_btn);
        _on.normal.background = Tex(new Color(0.12f, 0.42f, 0.55f, 0.95f));
        _on.normal.textColor = Color.white;
    }

    static GUIStyle Make(int size, Color c)
    {
        var s = new GUIStyle { fontSize = size, richText = true, wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
        s.normal.textColor = c;
        s.padding = new RectOffset(4, 4, 2, 2);
        return s;
    }

    static Texture2D Tex(Color c)
    {
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    static void Head(string t)
    {
        GUILayout.Space(4f);
        GUILayout.Label(t, _title);
    }

    static void Note(string t) => GUILayout.Label(t, _small);

    static void Toggle(string label, ref bool on, string sec, string key)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, _lab);
        bool hit = GUILayout.Button(on ? Opt.T("On", "开") : Opt.T("Off", "关"), on ? _on : _tab, GUILayout.Width(64f), GUILayout.Height(24f));
        GUILayout.EndHorizontal();
        if (!hit) return;
        on = !on;
        Opt.Save(sec, key, on);
    }

    static float Slider(string label, float v, float min, float max, string fmt)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, _lab, GUILayout.Width(120f));
        float n = GUILayout.HorizontalSlider(v, min, max);
        GUILayout.Label(n.ToString(fmt), _small, GUILayout.Width(72f));
        GUILayout.EndHorizontal();
        return n;
    }

    static float Log(string label, float v, float min, float max)
    {
        float a = Mathf.Log10(min), b = Mathf.Log10(max);
        float x = Mathf.Log10(Mathf.Clamp(v, min, max));
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, _lab, GUILayout.Width(120f));
        float n = GUILayout.HorizontalSlider(x, a, b);
        float r = Mathf.Clamp(Mathf.Pow(10f, n), min, max);
        if (Mathf.Abs(r - 1f) < 0.04f) r = 1f;
        GUILayout.Label(r.ToString(r >= 1f ? "0.00" : "0.000") + "x", _small, GUILayout.Width(72f));
        GUILayout.EndHorizontal();
        return r;
    }

    static void Fine(string label, ref float field, string sec, string key, float min, float max)
    {
        float n = Slider(label, field, min, max, "0.00");
        float step = (max - min) / 20f;
        if (Mathf.Abs(n - field) < step * 0.45f) return;
        field = Mathf.Clamp(min + Mathf.Round((n - min) / step) * step, min, max);
        Opt.Save(sec, key, field);
    }

    static void Step(string label, ref float field, string sec, string key, float min, float max)
    {
        float n = Slider(label, field, min, max, "0");
        if (Mathf.Abs(n - field) < 0.5f) return;
        field = Mathf.Clamp(Mathf.Round(n), min, max);
        Opt.Save(sec, key, field);
    }

    static int Pick(int cur, params string[] options)
    {
        int hit = -1;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < options.Length; i++)
            if (GUILayout.Button(options[i], i == cur ? _on : _tab, GUILayout.Height(24f))) hit = i;
        GUILayout.EndHorizontal();
        return hit;
    }
}
