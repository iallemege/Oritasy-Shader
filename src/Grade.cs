// Adapted from Boscali Summer (modules/Effects: DisplayGrade, Sharpening, CubeLut).
// MIT License, Copyright (c) 2026 GrabowMar. The full notice ships in the bag as THIRD_PARTY.txt.

using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OritasyShader;

/// <summary>
/// Client colour grade built only from what URP already ships: one global volume above the game's
/// own post-processing (LevelInfo.PostProcessing, priority 0.5), below Boscali Summer's grade
/// (0.75) and the game's night vision (1), plus RCAS sharpening. The game keeps what it owns:
/// ExposureController drives postExposure and GLOC drives ColorAdjustments.saturation on the game's
/// profile, so saturation here goes through the channel mixer instead.
/// </summary>
static class Grade
{
    const float Priority = 0.7f, Every = 0.25f;
    const float LumaR = 0.2126f, LumaG = 0.7152f, LumaB = 0.0722f;
    const string Boscali = "com.marci.boscalisummer";

    static GameObject _holder;
    static VolumeProfile _profile;
    static ColorAdjustments _adjust;
    static WhiteBalance _balance;
    static ChannelMixer _mixer;
    static ColorLookup _lookup;
    static Vignette _vignette;
    static FilmGrain _grain;
    static Bloom _bloom;
    static ChromaticAberration _chroma;
    static LiftGammaGain _lgg;
    static SplitToning _split;
    static Tonemapping _tone;
    static Texture2D _lut;
    static string _lutKey = "";
    static float _next;

    static UniversalRenderPipelineAsset _asset;
    static UpscalingFilterSelection _savedFilter;
    static bool _savedOverride;
    static float _savedSharp;

    public static string LutDir => Path.Combine(Opt.Root, "luts");

    public static void Tick()
    {
        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + Every;
        bool on = Opt.GradeOn;
        Sharpen(on && Opt.SharpOn && !Opt.Taken(Boscali), Opt.SharpAmt);
        LevelInfo level = null;
        try { level = NetworkSceneSingleton<LevelInfo>.i; }
        catch { }
        if (!on || level == null || level.PostProcessing == null)
        {
            Teardown();
            return;
        }
        Ensure(level);
        _adjust.contrast.Override(Mathf.Clamp(Opt.GradeContrast, -100f, 100f));
        _adjust.hueShift.Override(Mathf.Clamp(Opt.GradeHue, -180f, 180f));
        _balance.temperature.Override(Mathf.Clamp(Opt.GradeTemp, -100f, 100f));
        _balance.tint.Override(Mathf.Clamp(Opt.GradeTint, -100f, 100f));
        // Zero hands the effect back to the game's own volume (its bloom follows day and night).
        Optional(_vignette, _vignette.intensity, Opt.GradeVignette);
        Optional(_grain, _grain.intensity, Opt.GradeGrain);
        Optional(_bloom, _bloom.intensity, Opt.GradeBloom);
        if (_bloom.active)
        {
            _bloom.threshold.Override(Mathf.Clamp(Opt.GradeBloomThreshold, 0f, 2f));
            _bloom.scatter.Override(Mathf.Clamp01(Opt.GradeBloomScatter));
        }
        Optional(_chroma, _chroma.intensity, Opt.GradeChroma);

        // Lift / gamma / gain keep a white colour and move only the brightness offset (w).
        bool lgg = Mathf.Abs(Opt.GradeLift) + Mathf.Abs(Opt.GradeGamma) + Mathf.Abs(Opt.GradeGain) > 0.5f;
        _lgg.active = lgg;
        if (lgg)
        {
            _lgg.lift.Override(new Vector4(1f, 1f, 1f, Mathf.Clamp(Opt.GradeLift, -100f, 100f) / 200f));
            _lgg.gamma.Override(new Vector4(1f, 1f, 1f, Mathf.Clamp(Opt.GradeGamma, -100f, 100f) / 200f));
            _lgg.gain.Override(new Vector4(1f, 1f, 1f, Mathf.Clamp(Opt.GradeGain, -100f, 100f) / 200f));
        }
        bool split = Opt.GradeShadowTone > 0.5f || Opt.GradeHighTone > 0.5f;
        _split.active = split;
        if (split)
        {
            _split.shadows.Override(Tone(Opt.GradeShadowHue, Opt.GradeShadowTone));
            _split.highlights.Override(Tone(Opt.GradeHighHue, Opt.GradeHighTone));
            _split.balance.Override(Mathf.Clamp(Opt.GradeSplitBalance, -100f, 100f));
        }
        _tone.active = Opt.GradeTonemap > 0;
        if (_tone.active) _tone.mode.Override(Opt.GradeTonemap == 1 ? TonemappingMode.Neutral : TonemappingMode.ACES);

        // out = luma * (1 - s) + colour * s, written as mixer percentages.
        float s = 1f + Mathf.Clamp(Opt.GradeSat, -100f, 100f) / 100f;
        float grey = (1f - s) * 100f, own = s * 100f;
        _mixer.redOutRedIn.Override(LumaR * grey + own);
        _mixer.redOutGreenIn.Override(LumaG * grey);
        _mixer.redOutBlueIn.Override(LumaB * grey);
        _mixer.greenOutRedIn.Override(LumaR * grey);
        _mixer.greenOutGreenIn.Override(LumaG * grey + own);
        _mixer.greenOutBlueIn.Override(LumaB * grey);
        _mixer.blueOutRedIn.Override(LumaR * grey);
        _mixer.blueOutGreenIn.Override(LumaG * grey);
        _mixer.blueOutBlueIn.Override(LumaB * grey + own);

        Texture2D strip = Lut();
        _lookup.texture.Override(strip);
        _lookup.contribution.Override(strip != null ? Mathf.Clamp01(Opt.GradeLutMix) : 0f);
    }

    /// <summary>Split toning colour: grey is neutral, so the hue is mixed in from grey by the strength.</summary>
    static Color Tone(float hue, float amount) =>
        Color.Lerp(Color.grey, Color.HSVToRGB(Mathf.Repeat(hue, 360f) / 360f, 1f, 1f), Mathf.Clamp01(amount / 100f));

    static void Optional(VolumeComponent c, FloatParameter p, float v)
    {
        bool on = v > 0.001f;
        c.active = on;
        if (on) p.Override(v);
        else p.overrideState = false;
    }

    static void Ensure(LevelInfo level)
    {
        if (_holder != null) return;
        if (_profile != null) UnityEngine.Object.Destroy(_profile);
        _holder = new GameObject("OritasyShader.Grade");
        // Cameras only blend volumes on layers in their volume mask: share the game's.
        _holder.layer = level.PostProcessing.gameObject.layer;
        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _adjust = _profile.Add<ColorAdjustments>();
        _balance = _profile.Add<WhiteBalance>();
        _mixer = _profile.Add<ChannelMixer>();
        _lookup = _profile.Add<ColorLookup>();
        _vignette = _profile.Add<Vignette>();
        _grain = _profile.Add<FilmGrain>();
        _grain.type.Override(FilmGrainLookup.Medium1);
        _bloom = _profile.Add<Bloom>();
        _chroma = _profile.Add<ChromaticAberration>();
        _lgg = _profile.Add<LiftGammaGain>();
        _split = _profile.Add<SplitToning>();
        _tone = _profile.Add<Tonemapping>();
        Volume v = _holder.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = Priority;
        v.sharedProfile = _profile;
    }

    static void Teardown()
    {
        if (_holder != null) UnityEngine.Object.Destroy(_holder);
        if (_profile != null) UnityEngine.Object.Destroy(_profile);
        _holder = null;
        _profile = null;
        _lutKey = "";
        DropLut();
    }

    /// <summary>The configured .cube as a strip of the pipeline's LUT size; reloads when the name, size or file changes.</summary>
    static Texture2D Lut()
    {
        string name = Path.GetFileName(Opt.GradeLut ?? "");
        UniversalRenderPipelineAsset pipe = UniversalRenderPipeline.asset;
        if (name.Length == 0 || pipe == null)
        {
            _lutKey = "";
            DropLut();
            return null;
        }
        if (!Path.HasExtension(name)) name += ".cube";
        string path = Path.Combine(LutDir, name);
        int size = pipe.colorGradingLutSize;
        string key;
        try { key = path + "|" + size + "|" + System.IO.File.GetLastWriteTimeUtc(path).Ticks; }
        catch { key = path + "|" + size; }
        if (key == _lutKey) return _lut;
        _lutKey = key;
        DropLut();
        try
        {
            int cubeSize = 0;
            float[] cube = System.IO.File.Exists(path) ? CubeLut.Parse(System.IO.File.ReadAllText(path), out cubeSize) : null;
            if (cube == null)
            {
                Debug.LogWarning("[OritasyShader GRADE] No readable 3D .cube LUT at " + path);
                return null;
            }
            // Linear format: URP indexes the user LUT with sRGB-encoded colour and wants the stored values raw.
            _lut = new Texture2D(size * size, size, TextureFormat.RGBA32, false, true)
            {
                name = "OritasyShader.Lut",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            _lut.LoadRawTextureData(CubeLut.Bake(cube, cubeSize, size));
            _lut.Apply(false, true);
            Debug.Log("[OritasyShader GRADE] LUT " + name + " (" + cubeSize + "^3) loaded at " + size + "^3.");
        }
        catch (Exception e)
        {
            DropLut();
            Debug.LogWarning("[OritasyShader GRADE] LUT load failed: " + e.Message);
        }
        return _lut;
    }

    static void DropLut()
    {
        if (_lut != null) UnityEngine.Object.Destroy(_lut);
        _lut = null;
    }

    /// <summary>
    /// AMD FidelityFX RCAS with no shader of our own. URP treats the FSR filter as upscaling even at
    /// 100 % render scale, so selecting it on the pipeline asset runs RCAS in the final blit. The
    /// asset's three fields are captured on first use and put back when sharpening goes off.
    /// </summary>
    static void Sharpen(bool on, float strength)
    {
        var cur = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        // A quality-level change can swap the asset under us: hand the old one back first.
        if (_asset != null && _asset != cur) Restore();
        if (!on || cur == null)
        {
            Restore();
            return;
        }
        if (_asset == null)
        {
            _asset = cur;
            _savedFilter = cur.upscalingFilter;
            _savedOverride = cur.fsrOverrideSharpness;
            _savedSharp = cur.fsrSharpness;
        }
        cur.upscalingFilter = UpscalingFilterSelection.FSR;
        cur.fsrOverrideSharpness = true;
        cur.fsrSharpness = Mathf.Clamp01(strength);
    }

    public static void Restore()
    {
        if (_asset == null) return;
        _asset.upscalingFilter = _savedFilter;
        _asset.fsrOverrideSharpness = _savedOverride;
        _asset.fsrSharpness = _savedSharp;
        _asset = null;
    }
}

/// <summary>
/// Chromatic aberration that climbs ahead of a G blackout and settles back after. The game greys
/// the picture below blood pressure 0.7, blacks it out from 0.6 and takes the pilot under at 0.2;
/// the fringes start at 0.9, peak by 0.5 and hold while unconscious. Its own volume sits just above
/// the grade, so it works with the grade off and returns to the configured value when it fades.
/// </summary>
static class Gee
{
    const float Priority = 0.72f, Start = 0.9f, Full = 0.5f, Peak = 1f, Rise = 2.5f, Fall = 0.7f;

    static readonly AccessTools.FieldRef<GLOC, float> Pressure = AccessTools.FieldRefAccess<GLOC, float>("bloodPressure");
    static readonly AccessTools.FieldRef<GLOC, bool> Awake = AccessTools.FieldRefAccess<GLOC, bool>("conscious");

    static GameObject _holder;
    static VolumeProfile _profile;
    static ChromaticAberration _chroma;
    static GLOC _gloc;
    static float _k, _findAt;

    public static void Tick()
    {
        float want = Opt.InMatch ? Strain() : 0f;
        float dt = Time.unscaledDeltaTime;
        _k = want > _k ? Mathf.MoveTowards(_k, want, Rise * dt) : Mathf.MoveTowards(_k, want, Fall * dt);
        if (_k <= 0.002f)
        {
            if (_holder != null) _holder.SetActive(false);
            return;
        }
        if (!Ensure()) return;
        float own = Opt.GradeOn ? Mathf.Clamp01(Opt.GradeChroma) : 0f;
        _chroma.intensity.Override(Mathf.Lerp(own, Peak, _k));
        if (!_holder.activeSelf) _holder.SetActive(true);
    }

    static float Strain()
    {
        if (Opt.Plane() == null) return 0f;
        float now = Time.unscaledTime;
        if (_gloc == null && now >= _findAt)
        {
            _findAt = now + 2f;
            _gloc = UnityEngine.Object.FindObjectOfType<GLOC>();
        }
        if (_gloc == null) return 0f;
        try
        {
            if (!Awake(_gloc)) return 1f;
            return Mathf.Clamp01((Start - Pressure(_gloc)) / (Start - Full));
        }
        catch { return 0f; }
    }

    static bool Ensure()
    {
        if (_holder != null) return true;
        LevelInfo level = null;
        try { level = NetworkSceneSingleton<LevelInfo>.i; }
        catch { }
        if (level == null || level.PostProcessing == null) return false;
        if (_profile != null) UnityEngine.Object.Destroy(_profile);
        _holder = new GameObject("OritasyShader.Gee");
        _holder.layer = level.PostProcessing.gameObject.layer;
        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _chroma = _profile.Add<ChromaticAberration>();
        Volume v = _holder.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = Priority;
        v.sharedProfile = _profile;
        return true;
    }
}

/// <summary>
/// Scene light on top of the game's day and night. LevelInfo.UpdateTimeOfDayLighting rewrites the
/// sun colour (brightness is folded into it), sky light, reflections and fog from its curves once a
/// second; right after each rewrite they are scaled, so nothing compounds. Shadow strength and
/// distance are not touched by the game and are set outright, with the originals put back at 0.
/// </summary>
static class SceneLight
{
    const float Every = 0.25f;
    static readonly FieldInfo LastLit = AccessTools.Field(typeof(LevelInfo), "lightingLastUpdated");
    static readonly AccessTools.FieldRef<LevelInfo, float> AmbientField = AccessTools.FieldRefAccess<LevelInfo, float>("ambientIntensity");
    static float _seen = -1f, _next;

    static Light _shadowLight;
    static float _savedShadow;
    static UniversalRenderPipelineAsset _asset;
    static float _savedDistance;

    public static void Apply(LevelInfo li)
    {
        if (li == null || LastLit == null) return;
        float lit = (float)LastLit.GetValue(li);
        if (lit == _seen) return;
        _seen = lit;
        float sun = Mathf.Clamp(Opt.LightSun, 0.3f, 2f), sky = Mathf.Clamp(Opt.LightSky, 0.3f, 2f), fog = Mathf.Clamp(Opt.LightFog, 0f, 3f);
        if (Mathf.Abs(sun - 1f) > 0.001f && li.sun != null) li.sun.color *= sun;
        if (Mathf.Abs(sky - 1f) > 0.001f)
        {
            RenderSettings.ambientSkyColor *= sky;
            RenderSettings.ambientEquatorColor *= sky;
            RenderSettings.ambientGroundColor *= sky;
            RenderSettings.ambientIntensity *= sky;
            RenderSettings.reflectionIntensity *= sky;
            AmbientField(li) *= sky;
        }
        if (Mathf.Abs(fog - 1f) > 0.001f) RenderSettings.fogDensity *= fog;
    }

    public static void Tick()
    {
        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + Every;
        LevelInfo li = null;
        if (Opt.InMatch)
            try { li = NetworkSceneSingleton<LevelInfo>.i; }
            catch { }
        Shadows(li != null ? li.sun : null, Opt.LightShadow);
        Distance(Opt.LightShadowKm);
    }

    static void Shadows(Light sun, float strength)
    {
        if (_shadowLight != null && _shadowLight != sun) PutShadowBack();
        if (sun == null || strength <= 0.001f)
        {
            PutShadowBack();
            return;
        }
        if (_shadowLight == null)
        {
            _shadowLight = sun;
            _savedShadow = sun.shadowStrength;
        }
        sun.shadowStrength = Mathf.Clamp01(strength);
    }

    static void PutShadowBack()
    {
        if (_shadowLight != null) _shadowLight.shadowStrength = _savedShadow;
        _shadowLight = null;
    }

    static void Distance(float km)
    {
        var cur = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (_asset != null && _asset != cur) Restore();
        if (cur == null || km <= 0.001f)
        {
            Restore();
            return;
        }
        if (_asset == null)
        {
            _asset = cur;
            _savedDistance = cur.shadowDistance;
        }
        cur.shadowDistance = Mathf.Clamp(km, 0.1f, 8f) * 1000f;
    }

    public static void Restore()
    {
        PutShadowBack();
        if (_asset == null) return;
        _asset.shadowDistance = _savedDistance;
        _asset = null;
    }
}

[HarmonyPatch(typeof(LevelInfo), "UpdateTimeOfDayLighting")]
static class H_SceneLight
{
    static void Postfix(LevelInfo __instance)
    {
        try { SceneLight.Apply(__instance); }
        catch (Exception e) { Debug.LogWarning(e); }
    }
}

/// <summary>
/// Reads the .cube 3D LUT text format (Resolve, Photoshop, every free LUT pack) and resamples it to
/// the strip URP's Color Lookup accepts: size*size wide, size high, blue picks the slice, red runs
/// along x inside it, green up y.
/// </summary>
static class CubeLut
{
    public const int MaxSize = 65;

    /// <summary>RGB triples, red fastest, then green, then blue. Null when the text is not a complete 3D LUT.</summary>
    public static float[] Parse(string text, out int size)
    {
        size = 0;
        if (string.IsNullOrEmpty(text)) return null;
        float[] data = null;
        int filled = 0;
        char[] blanks = { ' ', '\t' };
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            string[] parts = line.Split(blanks, StringSplitOptions.RemoveEmptyEntries);
            if (char.IsLetter(line[0]))
            {
                // TITLE and DOMAIN_MIN/MAX are skipped, so the 0..1 domain of display LUTs is assumed.
                if (parts[0] != "LUT_3D_SIZE") continue;
                if (parts.Length < 2 || !int.TryParse(parts[1], out size) || size < 2 || size > MaxSize) return null;
                data = new float[size * size * size * 3];
                continue;
            }
            if (data == null || parts.Length < 3 || filled + 3 > data.Length) return null;
            for (int c = 0; c < 3; c++)
                if (!float.TryParse(parts[c], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out data[filled++]))
                    return null;
        }
        return data != null && filled == data.Length ? data : null;
    }

    /// <summary>RGBA32 rows for a size*size by size strip, first row = green 0, trilinear resample.</summary>
    public static byte[] Bake(float[] cube, int cubeSize, int size)
    {
        var pixels = new byte[size * size * size * 4];
        float step = (cubeSize - 1) / (float)(size - 1);
        int at = 0;
        for (int g = 0; g < size; g++)
            for (int b = 0; b < size; b++)
                for (int r = 0; r < size; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        float value = Sample(cube, cubeSize, r * step, g * step, b * step, c);
                        pixels[at++] = (byte)(Math.Min(1f, Math.Max(0f, value)) * 255f + 0.5f);
                    }
                    pixels[at++] = 255;
                }
        return pixels;
    }

    static float Sample(float[] cube, int n, float r, float g, float b, int channel)
    {
        int r0 = Math.Min((int)r, n - 1), g0 = Math.Min((int)g, n - 1), b0 = Math.Min((int)b, n - 1);
        int r1 = Math.Min(r0 + 1, n - 1), g1 = Math.Min(g0 + 1, n - 1), b1 = Math.Min(b0 + 1, n - 1);
        float tr = r - r0, tg = g - g0, tb = b - b0;
        float At(int ri, int gi, int bi) => cube[((bi * n + gi) * n + ri) * 3 + channel];
        float Mix(float from, float to, float t) => from + (to - from) * t;
        float low = Mix(Mix(At(r0, g0, b0), At(r1, g0, b0), tr), Mix(At(r0, g1, b0), At(r1, g1, b0), tr), tg);
        float high = Mix(Mix(At(r0, g0, b1), At(r1, g0, b1), tr), Mix(At(r0, g1, b1), At(r1, g1, b1), tr), tg);
        return Mix(low, high, tb);
    }
}
