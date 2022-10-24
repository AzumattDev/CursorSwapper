using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CursorSwapper
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class CursorSwapperPlugin : BaseUnityPlugin
    {
        internal const string ModName = "CursorSwapper";
        internal const string ModVersion = "1.1.0";
        internal const string Author = "Azumatt";
        private const string ModGuid = Author + "." + ModName;
        private const string ConfigFileName = ModGuid + ".cfg";

        private static readonly string ConfigFileFullPath =
            Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        internal static string ConnectionError = "";

        private readonly Harmony _harmony = new(ModGuid);

        private static readonly ManualLogSource CursorSwapperLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static Texture2D _cursorSprite = null!;
        private static Texture2D _vanillaCursorSprite = null!;

        private enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            _useCustomCursor = Config.Bind("1 - General", "Use Custom Cursor", Toggle.On,
                "If set to on, the mod will attempt to search for a cursor.png file located in the plugins folder.\nIf it's not found, a warning will be presented in the console and the default game cursor will be used.");

            _vanillaCursorSprite = Resources.FindObjectsOfTypeAll<Texture2D>()
                .First(s => s.name == "cursor" && s.isReadable);
            if (_useCustomCursor.Value == Toggle.On)
            {
                ApplyCursor();
            }

            _useCustomCursor.SettingChanged += (_, _) =>
            {
                if (_useCustomCursor.Value == Toggle.Off)
                {
                    Cursor.SetCursor(_vanillaCursorSprite, Vector2.zero, CursorMode.Auto);
                }
                else
                {
                    ApplyCursor();
                }
            };


            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;

            FileSystemWatcher folderWatcher =
                new(Paths.PluginPath);
            folderWatcher.Changed += UpdateCursor;
            folderWatcher.Created += UpdateCursor;
            folderWatcher.Deleted += UpdateCursor;
            folderWatcher.Renamed += UpdateCursor;
            folderWatcher.Error += OnError;
            folderWatcher.IncludeSubdirectories = true;
            folderWatcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            folderWatcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                CursorSwapperLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                CursorSwapperLogger.LogError($"There was an issue loading your {ConfigFileName}");
                CursorSwapperLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        private static void UpdateCursor(object sender, FileSystemEventArgs e)
        {
            if (_useCustomCursor.Value == Toggle.On)
                ApplyCursor();
        }

        private static void OnError(object sender, ErrorEventArgs e) =>
            PrintException(e.GetException());

        private static void PrintException(Exception? ex)
        {
            while (true)
            {
                if (ex != null)
                {
                    CursorSwapperLogger.LogError($"Message: {ex.Message}");
                    CursorSwapperLogger.LogError("Stacktrace:");
                    CursorSwapperLogger.LogError(ex.StackTrace);
                    ex = ex.InnerException;
                    continue;
                }

                break;
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _useCustomCursor = null!;
        #endregion

        private static void ApplyCursor()
        {
            _cursorSprite = LoadTexture("cursor.png");
            if (_cursorSprite != null)
            {
                Cursor.SetCursor(_cursorSprite, new Vector2(6, 5), CursorMode.Auto);
            }
            else if (_vanillaCursorSprite != null)
            {
                // Reset to vanilla if the image isn't found.
                Cursor.SetCursor(_vanillaCursorSprite, Vector2.zero, CursorMode.Auto);
            }
        }

        private static Texture2D LoadTexture(string name)
        {
            Texture2D texture = new(0, 0);

            string? directoryName = Path.GetDirectoryName(Paths.PluginPath);
            if (directoryName == null) return texture;
            List<string> paths = Directory.GetFiles(directoryName, "cursor.png", SearchOption.AllDirectories)
                .OrderBy(Path.GetFileName).ToList();
            try
            {
                byte[] fileData = File.ReadAllBytes(paths.Find(x => x.Contains(name)));
                texture.LoadImage(fileData);
            }
            catch
            {
                CursorSwapperLogger.LogWarning(
                    $"The file {name} couldn't be found in the directory path. Please make sure you are naming your files correctly and they are location somewhere in the BepInEx/plugins folder.\n" +
                    $"Optionally, you can turn off 'Use Custom Cursor' inside of your configuration file. If you no longer wish to see this error.");
                texture = _vanillaCursorSprite;
            }


            return texture!;
        }
    }
}