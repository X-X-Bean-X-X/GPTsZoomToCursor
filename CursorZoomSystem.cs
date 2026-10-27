using System;
using System.Reflection;
using Colossal.Mathematics;
using Game;
using Game.Input;
using Game.Rendering;
using Game.SceneFlow;
using Game.Simulation;
using Game.UI.InGame;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;

namespace GPTZoomToCursor
{
    [Preserve]
    public sealed class CursorZoomSystem : GameSystemBase
    {
        private CameraUpdateSystem cameras;
        private InputManager previousInput;
        private ProxyActionMap cameraMap;
        private ProxyAction zoomAction, moveAction, fastAction, rotateAction;
        private bool failed;
        private bool mouseZoomActive;
        private readonly NativeInputPeek inputPeek = new NativeInputPeek();
        private readonly TiltMotion tiltMotion = new TiltMotion();
        private CameraController tiltCamera;
        public int AppliedFrames { get; private set; }
        private static readonly FieldInfo GameplaySettings = Field("m_CameraSettings");
        private static readonly FieldInfo LegacyTilt = Field("m_TiltRange");
        private static readonly FieldInfo MoveSmoothing = Field("m_MoveSmoothing");
        private static readonly FieldInfo MapTransition = Field("m_MapViewTimer");
        private static readonly PropertyInfo SourceAction = typeof(ProxyAction).GetProperty("sourceAction", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo Field(string name) => typeof(CameraController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            cameras = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
            if (GameplaySettings == null || LegacyTilt == null || MoveSmoothing == null || MapTransition == null || SourceAction == null)
            {
                failed = true;
                Mod.Log.Error("Camera API has changed. Cursor zoom disabled; standard camera retained.");
            }
        }

        [Preserve]
        protected override void OnUpdate()
        {
            AutomaticTilt.Restore();
            if (!Mod.Loaded || Mod.Options == null || !Mod.Options.EnableMod || failed) { mouseZoomActive = false; tiltMotion.Reset(); return; }
            if (!Mod.Options.EnableAutomaticCameraTilt) tiltMotion.Reset();
            try
            {
                AutomaticTilt.Apply(cameras);
                UpdateZoom();
            }
            catch (Exception error)
            {
                AutomaticTilt.Restore();
                failed = true;
                Mod.Log.Error(error, "Cursor zoom stopped after an unexpected error. Standard camera retained; see log.");
            }
        }

        [Preserve]
        protected override void OnDestroy()
        {
            AutomaticTilt.Restore();
            base.OnDestroy();
        }

        private void UpdateZoom()
        {
            InputManager input = InputManager.instance;
            if (input == null || !Application.isFocused || !input.controlOverWorld || input.hasInputFieldFocus || input.overlayActive
                || input.activeControlScheme != InputManager.ControlScheme.KeyboardAndMouse
                || Mouse.current == null) { mouseZoomActive = false; tiltMotion.Reset(); return; }
            if (math.abs(Mouse.current.scroll.ReadValue().y) > 0.001f) mouseZoomActive = true;
            if (!mouseZoomActive) return;

            CameraController camera = cameras.gamePlayController;
            Camera output = cameras.activeCamera;
            if (camera == null || output == null || output.orthographic || !camera.controllerEnabled || !camera.inputEnabled
                || !ReferenceEquals(cameras.activeCameraController, camera) || MapTilesUISystem.mapTileViewActive
                || (float)MapTransition.GetValue(camera) > 0f) { mouseZoomActive = false; tiltMotion.Reset(); return; }
            if (!ReferenceEquals(tiltCamera, camera)) { tiltMotion.Reset(); tiltCamera = camera; }

            if (!ReferenceEquals(previousInput, input))
            {
                cameraMap = input.FindActionMap("Camera");
                zoomAction = cameraMap.FindAction("Zoom");
                moveAction = cameraMap.FindAction("Move");
                fastAction = cameraMap.FindAction("Move Fast");
                rotateAction = cameraMap.FindAction("Rotate");
                previousInput = input;
            }
            if (!cameraMap.enabled || !zoomAction.enabled) { mouseZoomActive = false; return; }
            // The native zoom processor has a smoothing tail after raw wheel delta returns to zero.
            // Follow its processed value, but only when the active binding belongs to the mouse.
            var source = SourceAction.GetValue(zoomAction) as InputAction;
            if (source == null) return;
            if (source.activeControl != null && !(source.activeControl.device is Mouse)) { mouseZoomActive = false; return; }
            Vector3 mouse = input.mousePosition;
            float margin = math.max(2f, Screen.height * 0.01f);
            if (camera.edgeScrolling && camera.edgeScrollingSensitivity > 0f
                && (mouse.x <= margin || mouse.y <= margin || mouse.x >= Screen.width - margin || mouse.y >= Screen.height - margin)) return;
            float zoomInput = inputPeek.ReadZoom(source, zoomAction, moveAction, fastAction, rotateAction, out bool moving);
            if (math.abs(zoomInput) <= 1E-07f) { mouseZoomActive = false; return; }
            if (moving) { mouseZoomActive = false; tiltMotion.Reset(); return; }
            float before = camera.zoom;
            float after = math.clamp(math.pow(before, 1f + zoomInput), camera.zoomRange.min, camera.zoomRange.max);
            if (Mod.Options.EnableAutomaticCameraTilt && !camera.useLegacyCamera
                && !GameManager.instance.gameMode.IsEditor())
            {
                float2 angle = camera.angle;
                angle.y = tiltMotion.Step(before, after, camera.zoomRange.min, angle.y);
                camera.angle = angle;
            }
            if (!ZoomMath.ShouldAnchor(Mod.Options.EnableMod, Mod.Options.ZoomOutFromCursor, before, after)) return;

            TerrainSystem terrain = camera.terrainSystem;
            if (terrain == null) return;
            TerrainHeightData heights = terrain.GetHeightData();
            if (!heights.isCreated) return;
            Ray ray = output.ScreenPointToRay(mouse);
            if (ray.direction.y >= -0.025f) return;
            var segment = new Line3.Segment((float3)ray.origin, (float3)(ray.origin + ray.direction * 50000f));
            if (!TerrainUtils.Raycast(ref heights, segment, true, out float t, out float3 normal, out Bounds3 hitBounds)
                || t < 0f || t > 1f) return;
            float3 anchor = math.lerp(segment.a, segment.b, t);

            WaterSurfacesData water = default;
            WaterSurfaceData<half> maxWater = default;
            WaterSystem waterSystem = camera.waterSystem;
            if (waterSystem != null && waterSystem.Loaded)
            {
                water = waterSystem.GetSurfacesData(out var waterDeps);
                waterDeps.Complete();
                maxWater = waterSystem.GetMexHeightSurfaceData(out var maxDeps);
                maxDeps.Complete();
            }
            // Over water, anchor the visible surface rather than the lake bed.
            if (water.depths.isCreated)
            {
                for (int i = 0; i < 3; i++)
                {
                    float surface = Surface(ref heights, ref water, ref maxWater, anchor);
                    float distance = (surface - ray.origin.y) / ray.direction.y;
                    if (distance <= 0f || distance > 50000f) return;
                    anchor = (float3)(ray.origin + ray.direction * distance);
                }
            }

            float2 nextAngle = camera.angle;
            if (camera.useLegacyCamera)
            {
                float2 limits = (float2)LegacyTilt.GetValue(camera);
                nextAngle.y = math.clamp(nextAngle.y, limits.x, limits.y);
            }
            else
            {
                var settings = (CameraSettingsGameplay)GameplaySettings.GetValue(camera);
                if (settings == null) return;
                nextAngle.y = math.clamp(nextAngle.y, settings.MinTiltZoomCurve.Evaluate(after), settings.TiltRange.y);
            }
            Quaternion rotation = Quaternion.Euler(nextAngle.y, nextAngle.x, 0f);
            Vector3 localDirection = Quaternion.Inverse(output.transform.rotation) * ray.direction;
            float3 direction = rotation * localDirection;
            float3 offset = rotation * new Vector3(0f, 0f, -after);
            float3 originalPivot = camera.pivot;
            float3 candidate = originalPivot;
            float smoothing = (float)MoveSmoothing.GetValue(camera);
            Bounds3 bounds = GameManager.instance.gameMode.IsEditor()
                ? TerrainUtils.GetEditorCameraBounds(terrain, ref heights) : TerrainUtils.GetBounds(ref heights);
            float maxCorrection = math.min(2000f, math.max(10f, before * 0.75f));
            // Predict the native terrain/water clearance; never bypass the actual camera's collision pass.
            for (int i = 0; i < 5; i++)
            {
                float3 predictedPivot = candidate;
                predictedPivot.y = math.lerp(Surface(ref heights, ref water, ref maxWater, candidate), originalPivot.y, smoothing);
                predictedPivot = MathUtils.Clamp(predictedPivot, bounds);
                float3 origin = predictedPivot + offset;
                origin.y += camera.zoomRange.min * 0.5f;
                float floor = Surface(ref heights, ref water, ref maxWater, origin)
                    + camera.zoomRange.min * 0.5f + (after - camera.zoomRange.min) * 0.1f;
                float clearance = (origin.y - floor) / after;
                origin.y += (math.sqrt(clearance * clearance + 0.2f) - clearance) * (0.5f * after);
                if (!ZoomMath.TryPlaneCorrection(origin, direction, anchor, out float3 correction)) return;
                candidate += correction;
                candidate = MathUtils.Clamp(candidate, bounds);
                candidate.y = originalPivot.y;
                if (!math.all(math.isfinite(candidate)) || math.distance(candidate, originalPivot) > maxCorrection) return;
                if (math.lengthsq(correction) < 0.0001f) break;
            }
            camera.pivot = candidate;
            AppliedFrames++;
        }

        private static float Surface(ref TerrainHeightData heights, ref WaterSurfacesData water, ref WaterSurfaceData<half> maxWater, float3 position)
            => water.depths.isCreated ? WaterUtils.SampleHeight(ref maxWater, ref water, ref heights, position) : TerrainUtils.SampleHeight(ref heights, position);
    }
}
