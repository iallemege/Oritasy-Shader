namespace OritasyShader;

/// <summary>Hortensia's sky and cloud decks from fx.bundle, plus the mission weather and cloud height.</summary>
static class Sky
{
    struct Deck
    {
        public float Base, Top, Cover, Dense, Type, Scale, Erode;
        public Deck(float b, float t, float c, float d, float type, float s, float e)
        { Base = b; Top = t; Cover = c; Dense = d; Type = type; Scale = s; Erode = e; }
    }

    static readonly Deck None = new(1000f, 2000f, 0f, 0f, 0f, 3000f, 0.5f);
    static readonly Deck[][] Profiles =
    {
        new[] { new Deck(1400f, 2400f, 0.28f, 1f, 0f, 2600f, 0.55f), None, new Deck(4200f, 4900f, 0.1f, 0.6f, 0f, 3500f, 0.6f), new Deck(9000f, 9800f, 0.25f, 0.5f, 2f, 9000f, 0f) },
        new[] { new Deck(1300f, 2800f, 0.42f, 1f, 0f, 2400f, 0.5f), None, new Deck(4000f, 4800f, 0.2f, 0.7f, 0f, 3500f, 0.55f), new Deck(8800f, 9600f, 0.3f, 0.5f, 2f, 9000f, 0f) },
        new[] { new Deck(900f, 3200f, 0.45f, 1f, 0f, 2800f, 0.4f), new Deck(500f, 2200f, 0.85f, 0.9f, 1f, 4000f, 0.35f), new Deck(3600f, 6500f, 0.75f, 0.8f, 1f, 5000f, 0.35f), new Deck(8500f, 10200f, 0.6f, 0.6f, 2f, 8000f, 0f) },
        new[] { new Deck(1100f, 2300f, 0.5f, 1f, 0f, 2000f, 0.5f), new Deck(700f, 1500f, 0.45f, 0.8f, 1f, 3200f, 0.45f), new Deck(4000f, 5200f, 0.35f, 0.6f, 1f, 4500f, 0.45f), new Deck(9000f, 10000f, 0.35f, 0.5f, 2f, 7000f, 0f) },
        new[] { new Deck(1100f, 11500f, 0.32f, 1f, 0f, 3200f, 0.45f), new Deck(500f, 1100f, 0.4f, 0.8f, 1f, 2500f, 0.45f), new Deck(3500f, 6000f, 0.45f, 0.7f, 1f, 4500f, 0.4f), new Deck(9500f, 11500f, 0.4f, 0.7f, 2f, 7000f, 0f) },
        new[] { None, new Deck(-50f, 600f, 0.95f, 0.6f, 1f, 3000f, 0.3f), new Deck(1500f, 2300f, 0.5f, 0.7f, 1f, 4000f, 0.4f), new Deck(9000f, 9800f, 0.1f, 0.4f, 2f, 9000f, 0f) },
    };

    static readonly Vector4[] _layer = new Vector4[4], _shape = new Vector4[4], _cells = new Vector4[8], _cellFx = new Vector4[8];
    static AssetBundle _bundle;
    static Material _clouds, _sky, _skybox, _vanilla;
    static GameObject _dome;
    static bool _looked, _hooked, _hidden;
    static Vector2 _drift, _cirrus;
    public static bool Running => _dome != null;

    public static void Tick()
    {
        LevelInfo li = Level();
        if (li != null) Weather(li);
        if (!Opt.InMatch || !Opt.SkyOn || !Ready())
        {
            Clear();
            return;
        }
        Camera cam = Camera.main;
        if (cam == null) return;
        Build();
        if (_dome == null) return;
        _dome.transform.position = cam.transform.position;
        Quality();
        Hide(li);
        Decks(li, cam);
        Light(li);
        PaintSky(li);
    }

    public static void Clear()
    {
        Restore();
        if (_skybox != null && RenderSettings.skybox == _skybox && _vanilla != null) RenderSettings.skybox = _vanilla;
        _vanilla = null;
        if (_dome != null) UnityEngine.Object.Destroy(_dome);
        _dome = null;
    }

    static LevelInfo Level()
    {
        try { return NetworkSceneSingleton<LevelInfo>.i; }
        catch { return null; }
    }

    static bool Host()
    {
        try
        {
            NetworkManagerNuclearOption nm = NetworkManagerNuclearOption.i;
            return nm != null && nm.Server != null && nm.Server.Active;
        }
        catch { return false; }
    }

    static void Weather(LevelInfo li)
    {
        if (!Host()) return;
        if (Opt.Weather >= 0f)
        {
            float w = Mathf.Clamp01(Opt.Weather);
            if (Mathf.Abs(li.conditions - w) > 0.01f)
                try { li.Networkconditions = w; } catch { li.conditions = w; }
        }
        if (Opt.CloudMeters > 0f && Mathf.Abs(li.cloudHeight - Opt.CloudMeters) > 2f)
            try { li.NetworkcloudHeight = Opt.CloudMeters; } catch { li.cloudHeight = Opt.CloudMeters; }
    }

    static bool Ready()
    {
        if (_looked) return _clouds != null;
        _looked = true;
        string path = Path.Combine(Paths.PluginPath, "OritasyShader", "fx.bundle");
        if (!File.Exists(path))
            path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "fx.bundle");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[Oritasy Shader] fx.bundle missing: " + path);
            return false;
        }
        _bundle = AssetBundle.LoadFromFile(path);
        if (_bundle == null) return false;
        _clouds = _bundle.LoadAsset<Material>("Clouds");
        _sky = _bundle.LoadAsset<Material>("Sky");
        _skybox = _bundle.LoadAsset<Material>("Skybox");
        if (_clouds != null && (_clouds.shader == null || !_clouds.shader.isSupported)) _clouds = null;
        Debug.Log("[Oritasy Shader] sky " + (_clouds != null));
        return _clouds != null;
    }

    static void Build()
    {
        if (_dome != null) return;
        if (!_hooked)
        {
            _hooked = true;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += (_, cam) =>
            {
                if (_dome != null && cam != null && (cam.cameraType == CameraType.Game || cam.cameraType == CameraType.Reflection))
                    _dome.transform.position = cam.transform.position;
            };
        }
        _dome = new GameObject("OritasyShader.Sky");
        Shell(_dome, "clouds", _clouds);
        if (_sky != null) Shell(_dome, "stars", _sky);
    }

    static void Shell(GameObject parent, string name, Material m)
    {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.Destroy(s.GetComponent<Collider>());
        s.name = name;
        s.transform.SetParent(parent.transform, false);
        s.transform.localScale = Vector3.one * 1000f;
        MeshRenderer mr = s.GetComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    static void Quality()
    {
        _clouds.SetFloat("_Steps", Mathf.Clamp(Opt.CloudSteps, 8f, 48f));
        _clouds.SetFloat("_LightSteps", Opt.CloudSteps >= 32f ? 4f : 3f);
        _clouds.SetFloat("_Detail", 0.3f);
    }

    static void Hide(LevelInfo li)
    {
        if (_hidden || li == null) return;
        _hidden = true;
        try { if (li.starfield != null) li.starfield.SetActive(false); } catch { }
    }

    static void Restore()
    {
        if (!_hidden) return;
        _hidden = false;
        LevelInfo li = Level();
        try { if (li != null && li.starfield != null) li.starfield.SetActive(true); } catch { }
    }

    static void Decks(LevelInfo li, Camera cam)
    {
        int wx = Mathf.Clamp(Opt.Wx, 0, Profiles.Length - 1);
        Deck[] want = Profiles[wx];
        float shift = 0f;
        if (Opt.CloudMeters > 0f)
        {
            float low = 99999f;
            for (int i = 0; i < 4; i++)
                if (want[i].Cover > 0.05f) low = Mathf.Min(low, want[i].Base);
            if (low < 90000f) shift = Opt.CloudMeters - low;
        }
        for (int i = 0; i < 4; i++)
        {
            Deck d = want[i];
            _layer[i] = new Vector4(d.Base + shift, d.Top + shift, Mathf.Clamp01(d.Cover * Opt.CloudCover), d.Dense * Opt.CloudDensity);
            _shape[i] = new Vector4(d.Type, d.Scale, d.Erode, wx >= 4 ? 1f : 0.4f);
            _cells[i] = Vector4.zero;
            _cellFx[i] = Vector4.zero;
        }
        for (int i = 4; i < 8; i++) { _cells[i] = Vector4.zero; _cellFx[i] = Vector4.zero; }
        Vector3 wind = Vector3.zero;
        try { wind = li.windVelocity; } catch { }
        float dt = Time.deltaTime;
        _drift += new Vector2(-wind.x, -wind.z) * dt;
        _cirrus += new Vector2(-wind.x, -wind.z) * dt * 2.5f;
        Shader.SetGlobalVector("_SsWind", new Vector4(_drift.x, _drift.y, _cirrus.x, _cirrus.y));
        Shader.SetGlobalVectorArray("_SsLayer", _layer);
        Shader.SetGlobalVectorArray("_SsShape", _shape);
        Shader.SetGlobalVectorArray("_SsCells", _cells);
        Shader.SetGlobalVectorArray("_SsCellFx", _cellFx);
        Shader.SetGlobalFloat("_SsCellCount", 0f);
        Shader.SetGlobalFloat("_SsMaxDist", Mathf.Clamp(Opt.CloudDistKm, 10f, 100f) * 1000f);
        Shader.SetGlobalVector("_SsEvolve", new Vector4(Time.time * 2.5f, Time.time * 1.5f, Time.time * 2.5f, 1f));
        float fov = cam.fieldOfView;
        Shader.SetGlobalFloat("_SsPix", 2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height));
        try
        {
            GlobalPosition g0 = Vector3.zero.ToGlobalPosition();
            Shader.SetGlobalVector("_SsOrigin", new Vector4(g0.x, g0.z, Datum.LocalSeaY, 0f));
        }
        catch { }
    }

    static void Light(LevelInfo li)
    {
        Light sun = li.sun;
        Vector3 to = sun != null ? -sun.transform.forward : Vector3.up;
        float bright = Opt.CloudLight;
        Color c = sun != null ? sun.color * sun.intensity * 2.2f * bright : Color.black;
        Shader.SetGlobalVector("_SsSunDir", new Vector4(to.x, to.y, to.z, 1f));
        Shader.SetGlobalVector("_SsSunColor", c);
        Color amb = RenderSettings.ambientSkyColor * bright;
        Shader.SetGlobalVector("_SsAmbient", new Vector4(amb.r, amb.g, amb.b, 0f));
        Color fog = RenderSettings.fogColor;
        Shader.SetGlobalVector("_SsFog", new Vector4(fog.r, fog.g, fog.b, RenderSettings.fog ? RenderSettings.fogDensity : 0f));
    }

    static void PaintSky(LevelInfo li)
    {
        if (_skybox == null) return;
        if (RenderSettings.skybox != _skybox)
        {
            if (_vanilla == null) _vanilla = RenderSettings.skybox;
            if (RenderSettings.skybox != _vanilla && _vanilla != null) return;
            RenderSettings.skybox = _skybox;
        }
        Vector3 toSun = li.sun != null ? -li.sun.transform.forward : Vector3.down;
        float e = Mathf.Asin(Mathf.Clamp(toSun.y, -1f, 1f)) * Mathf.Rad2Deg;
        float night = Mathf.InverseLerp(8f, -8f, e);
        float day = 1f - night;
        Vector4 air = Vector4.Lerp(new Vector4(1f, 1f, 0.76f, 1f), new Vector4(1.05f, 0.6f, 0.76f, 1f), day);
        float haze = Opt.Wx >= 4 ? 0.55f : Opt.Wx == 5 ? 0.7f : 0.2f;
        Shader.SetGlobalVector("_SsSun", new Vector4(toSun.x, toSun.y, toSun.z, 30f));
        Shader.SetGlobalVector("_SsMoon", new Vector4(0f, -1f, 0f, 0.2f * night));
        Shader.SetGlobalVector("_SsSkyA", air);
        Shader.SetGlobalVector("_SsSkyB", new Vector4(0.75f, 0.85f, 1f, haze));
        Shader.SetGlobalVector("_SsSkyC", new Vector4(0.95f, 1f, 1.05f, 1f));
        Shader.SetGlobalVector("_SsSkyD", new Vector4(0.2f, night, Opt.Wx >= 2 ? 0.4f : 0f, Mathf.Lerp(0.01f, 0.4f, day)));
        Shader.SetGlobalVector("_SsSkyE", new Vector4(0.8f, 0.85f, 0.9f, 0.55f));
        Shader.SetGlobalVector("_SsSkyF", new Vector4(night, night * 0.3f, night * 0.5f, 12f));
        Shader.SetGlobalVector("_SsNight", new Vector4(night, 0f, 0f, 1f));
    }
}
