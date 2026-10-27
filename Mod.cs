using System;
using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Colossal.Localization;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Rendering;
using Game.SceneFlow;
using Game.Settings;

namespace GPTZoomToCursor
{
    public sealed class Mod : IMod
    {
        public const string Id = "GPTZoomToCursor";
        public const string Title = "GPT's Zoom to Cursor by X_Bean_X";
        public const string OptionsTitle = "GPT\u2019s Zoom to Cursor";
        internal static readonly ILog Log = LogManager.GetLogger(Id).SetShowsErrorsInUI(false);
        public static Settings Options { get; private set; }
        internal static bool Loaded { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Options = new Settings(this);
            AssetDatabase.global.LoadSettings(Id, Options, new Settings(this));
            Options.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new Locale(Options));
            Loaded = true;
            updateSystem.UpdateBefore<CursorZoomSystem, CameraUpdateSystem>(SystemUpdatePhase.PreCulling);
            updateSystem.UpdateAfter<CameraSettingsRestoreSystem, CameraUpdateSystem>(SystemUpdatePhase.PreCulling);
            Log.Info(Title + " 0.3.0 loaded. Enabled=" + Options.EnableMod + "; Zoom out from cursor=" + Options.ZoomOutFromCursor
                + "; Enable automatic camera tilt=" + Options.EnableAutomaticCameraTilt);
        }

        public void OnDispose()
        {
            Loaded = false;
            AutomaticTilt.Restore();
            Options?.UnregisterInOptionsUI();
            Options = null;
        }
    }

    [FileLocation(Mod.Id)]
    public sealed class Settings : ModSetting
    {
        [SettingsUISection("General")]
        public bool EnableMod { get; set; }

        [SettingsUISection("General")]
        public bool ZoomOutFromCursor { get; set; }

        [SettingsUISection("General")]
        public bool EnableAutomaticCameraTilt { get; set; }

        public Settings(IMod mod) : base(mod) { SetDefaults(); }

        public override void SetDefaults()
        {
            EnableMod = true;
            ZoomOutFromCursor = false;
            EnableAutomaticCameraTilt = false;
        }
    }

    internal sealed class Locale : IDictionarySource
    {
        private readonly Settings settings;
        internal Locale(Settings value) { settings = value; }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { settings.GetSettingsLocaleID(), Mod.OptionsTitle },
                { settings.GetOptionGroupLocaleID("General"), "Camera" },
                { settings.GetOptionLabelLocaleID(nameof(Settings.EnableMod)), "Enable mod" },
                { settings.GetOptionDescLocaleID(nameof(Settings.EnableMod)), "Zoom in toward the ground under your cursor. Turn off to restore standard camera zoom and automatic tilt behavior." },
                { settings.GetOptionLabelLocaleID(nameof(Settings.ZoomOutFromCursor)), "Zoom out from cursor" },
                { settings.GetOptionDescLocaleID(nameof(Settings.ZoomOutFromCursor)), "Also anchor zooming out to the ground under your cursor. Off by default: zooming out uses the standard game camera. Requires Enable mod." },
                { settings.GetOptionLabelLocaleID(nameof(Settings.EnableAutomaticCameraTilt)), "Enable automatic camera tilt" },
                { settings.GetOptionDescLocaleID(nameof(Settings.EnableAutomaticCameraTilt)), "Smoothly tilt toward a straight-down view as you zoom in, reaching 90 degrees at maximum zoom-in. Zooming back out retraces the transition. Manual tilt takes priority and sets a new starting angle; this option does not restrict your viewing angle. Off by default: keep your chosen angle while zooming. Requires Enable mod and the normal gameplay camera." }
            };
        }
        public void Unload() { }
    }
}
