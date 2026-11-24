using GHelper.Helpers;
using System.Management;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Mouse-only config store. Reads and writes the same config.json used by
/// G-Helper so existing mouse settings (DPI, bindings, lighting) are kept.
/// </summary>
public static class AppConfig
{
    private static string configFile;
    private static string fallbackConfigFile;

    private static Dictionary<string, object> config = new Dictionary<string, object>();
    private static readonly System.Timers.Timer timer = new System.Timers.Timer(2000) { AutoReset = false };
    private static readonly object configLock = new();

    private static readonly JsonSerializerOptions LenientOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly Regex KeyValueRegex = new(
        @"""((?:\\.|[^""\\])*)""\s*:\s*(""(?:\\.|[^""\\])*""|-?\d+(?:\.\d+)?|true|false|null)");

    private static readonly Lazy<string> _model =
        new Lazy<string>(LoadModel, LazyThreadSafetyMode.ExecutionAndPublication);

    static AppConfig()
    {
        string configName = "config.json";
        string appPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHelper");
        string startupConfig = Path.Combine(Application.StartupPath.Trim('\\'), configName);

        fallbackConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GHelper", configName);

        configFile = File.Exists(startupConfig) ? startupConfig
        : ProcessHelper.IsRunningAsSystem() && File.Exists(fallbackConfigFile) ? fallbackConfigFile
        : Path.Combine(appPath, configName);

        Directory.CreateDirectory(Path.GetDirectoryName(configFile));

        if (!TryLoadConfig(configFile) && !TryRecoverConfig(configFile) && !TryLoadConfig(configFile + ".bak") && !TryLoadConfig(fallbackConfigFile)) Init();

        timer.Elapsed += Timer_Elapsed;
    }

    private static bool TryLoadConfig(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            config = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path), LenientOptions);
            Logger.WriteLine($"Config loaded from {path}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"Broken config {path}: {ex.Message}");
            return false;
        }
    }

    private static bool TryRecoverConfig(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            var pairs = new Dictionary<string, string>();
            foreach (Match m in KeyValueRegex.Matches(File.ReadAllText(path)))
                pairs["\"" + m.Groups[1].Value + "\""] = m.Groups[2].Value;

            if (pairs.Count == 0) return false;

            string rebuilt = "{" + string.Join(",", pairs.Select(p => p.Key + ":" + p.Value)) + "}";
            config = JsonSerializer.Deserialize<Dictionary<string, object>>(rebuilt, LenientOptions);
            Logger.WriteLine($"Recovered {pairs.Count} values from broken config {path}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"Config recovery failed {path}: {ex.Message}");
            return false;
        }
    }

    private static void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        timer.Stop();
        string jsonString;
        lock (configLock) jsonString = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        try
        {
            WriteAtomic(configFile, jsonString);
            SyncFallbackConfig();
        }
        catch (Exception ex) { Logger.WriteLine("Config write failed: " + ex.Message); }
    }

    private static void WriteAtomic(string path, string content)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        using (var fs = new FileStream(tmp, FileMode.Open, FileAccess.Write))
            fs.Flush(flushToDisk: true);
        if (File.Exists(path))
            File.Replace(tmp, path, path + ".bak");
        else
            File.Move(tmp, path);
    }

    private static void SyncFallbackConfig()
    {
        if (fallbackConfigFile is null || fallbackConfigFile == configFile) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fallbackConfigFile));
            File.Copy(configFile, fallbackConfigFile, overwrite: true);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private static string LoadModel()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                using (obj) return obj["Model"]?.ToString() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine(ex.Message);
        }
        return string.Empty;
    }

    public static string GetModel() => _model.Value;

    private static void Init()
    {
        config = new Dictionary<string, object>();
        File.WriteAllText(configFile, JsonSerializer.Serialize(config));
    }

    public static int Get(string name, int empty = -1)
    {
        lock (configLock)
            return config.TryGetValue(name, out var val) && int.TryParse(val?.ToString(), out int result)
            ? result : empty;
    }

    public static bool Is(string name) => Get(name) == 1;

    public static bool IsAuraSync() => Is("mouse_aura_sync");

    public static string GetString(string name, string empty = null)
    {
        lock (configLock)
            return config.TryGetValue(name, out var val) ? val?.ToString() : empty;
    }

    private static void Write()
    {
        timer.Stop();
        timer.Start();
    }

    public static void Set(string name, int value)
    {
        lock (configLock) config[name] = value;
        Write();
    }

    public static void Set(string name, string value)
    {
        lock (configLock) config[name] = value;
        Write();
    }
}
