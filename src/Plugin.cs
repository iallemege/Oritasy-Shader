namespace OritasyShader;

[BepInPlugin(Pack.Guid, Pack.Name, Pack.Version)]
public sealed class Plugin : BaseUnityPlugin
{
    Harmony _harmony;

    void Awake()
    {
        Opt.Bind(Config);
        _harmony = new Harmony(Pack.Guid);
        _harmony.PatchAll();
        Logger.LogInfo(Pack.Name + " " + Pack.Label + " loaded.");
    }

    void Update()
    {
        Menu.Tick();
        Clock.Tick();
        Sky.Tick();
        Grade.Tick();
        SceneLight.Tick();
        Gee.Tick();
        Hide.Tick();
    }

    void OnGUI() => Menu.Draw();

    void OnDestroy()
    {
        Sky.Clear();
        Grade.Restore();
        SceneLight.Restore();
        if (Time.timeScale > 0.0001f && Time.timeScale < 0.999f) Time.timeScale = 1f;
        _harmony?.UnpatchSelf();
    }
}

public static class Pack
{
    public const string Guid = "com.ial.oritasy.shader";
    public const string Name = "Oritasy's Shader";
    public const string Version = "0.1.0";
    public const string Label = "0.1";
}
