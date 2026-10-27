using System;
using System.Reflection;
using Game;
using Game.Rendering;
using Game.SceneFlow;
using Game.UI.InGame;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

namespace GPTZoomToCursor
{
    // Replace only the zoom-dependent lower tilt bound during the native camera pass.
    // Restore the original curve afterward; never edit its keys or saved game settings.
    internal static class AutomaticTilt
    {
        private static readonly FieldInfo SettingsField = typeof(CameraController).GetField("m_CameraSettings", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CurveField = typeof(CameraSettingsGameplay).GetField("m_MinTiltZoomCurve", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TransitionField = typeof(CameraController).GetField("m_MapViewTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static CameraSettingsGameplay owner;
        private static AnimationCurve original;
        private static AnimationCurve flat;
        private static float flatMinimum = float.NaN;
        internal static bool Active => owner != null;
        internal static int AppliedFrames { get; private set; }

        internal static void Apply(CameraUpdateSystem cameras)
        {
            // Both checkbox states remove the native zoom-dependent restriction.
            // The enabled option supplies a motion trajectory in CursorZoomSystem.
            if (SettingsField == null || CurveField == null || TransitionField == null)
                throw new MissingFieldException("Native automatic tilt API changed");
            CameraController camera = cameras.gamePlayController;
            if (camera == null || !camera.controllerEnabled || !ReferenceEquals(cameras.activeCameraController, camera)
                || camera.useLegacyCamera || GameManager.instance.gameMode.IsEditor()
                || MapTilesUISystem.mapTileViewActive || (float)TransitionField.GetValue(camera) > 0f) return;
            var settings = (CameraSettingsGameplay)SettingsField.GetValue(camera);
            if (settings == null || settings.MinTiltZoomCurve == null) return;
            float minimum = settings.TiltRange.x;
            if (!math.isfinite(minimum)) return;
            if (flat == null || minimum != flatMinimum)
            {
                flat = AnimationCurve.Linear(0f, minimum, 1f, minimum);
                flatMinimum = minimum;
            }
            original = settings.MinTiltZoomCurve;
            owner = settings;
            CurveField.SetValue(settings, flat);
            AppliedFrames++;
        }

        internal static void Restore()
        {
            if (owner == null) return;
            // Do not overwrite a curve another mod supplied during this camera pass.
            if (ReferenceEquals(CurveField.GetValue(owner), flat)) CurveField.SetValue(owner, original);
            owner = null;
            original = null;
        }
    }

    [Preserve]
    public sealed class CameraSettingsRestoreSystem : GameSystemBase
    {
        [Preserve]
        protected override void OnUpdate() { AutomaticTilt.Restore(); }

        [Preserve]
        protected override void OnDestroy()
        {
            AutomaticTilt.Restore();
            base.OnDestroy();
        }
    }
}
