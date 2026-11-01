using UnityEngine;
using UnityEngine.Rendering.Universal;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// Frames the 18 × 32 play field in a 9:16 viewport, looking down with a slight tilt so the
    /// low-poly models read as 3D. On a wide screen (PC, web) the viewport is centred and the
    /// space either side is left for the HUD's side panels; on a phone it fills the screen.
    /// A second, empty camera clears the whole screen behind it.
    /// </summary>
    public sealed class PlayfieldCamera : MonoBehaviour
    {
        /// <summary>Degrees the view leans forward from straight down.</summary>
        public const float Tilt = 20f;
        public const float FieldOfView = 34f;
        /// <summary>Extra field shown beyond the edges, so the ship's wingtips stay on screen.</summary>
        const float Margin = 0.4f;

        public Camera Cam { get; private set; }
        Camera _backdropClear;
        int _lastW, _lastH;

        /// <summary>The play field's viewport in screen pixels (origin bottom-left).</summary>
        public Rect PixelRect => Cam.pixelRect;

        public static PlayfieldCamera Attach(Camera cam)
        {
            if (!cam.TryGetComponent<PlayfieldCamera>(out var pc)) pc = cam.gameObject.AddComponent<PlayfieldCamera>();
            pc.Init(cam);
            return pc;
        }

        void Init(Camera cam)
        {
            Cam = cam;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Clear;
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 250f;
            cam.depth = 0f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;

            var clearGo = new GameObject("Screen Clear Camera");
            clearGo.transform.SetParent(transform, false);
            _backdropClear = clearGo.AddComponent<Camera>();
            _backdropClear.clearFlags = CameraClearFlags.SolidColor;
            _backdropClear.backgroundColor = Palette.Clear;
            _backdropClear.cullingMask = 0;
            _backdropClear.depth = -10f;
            _backdropClear.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            Fit();
        }

        void LateUpdate()
        {
            if (Screen.width != _lastW || Screen.height != _lastH) Fit();
        }

        /// <summary>Recompute the viewport and camera placement for the current screen size.</summary>
        public void Fit()
        {
            _lastW = Screen.width;
            _lastH = Screen.height;
            float screenAspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            const float target = Field.Width / Field.Height; // 9:16
            Rect r;
            if (screenAspect > target)
            {
                float w = target / screenAspect;
                r = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            else
            {
                float h = screenAspect / target;
                r = new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }
            Cam.rect = r;
            Cam.ResetAspect();

            // Find the closest camera distance (and the aim point along the field) at which every
            // corner of the field fits in the viewport, with the field centred top to bottom.
            var rot = Quaternion.Euler(90f - Tilt, 0f, 0f);
            transform.rotation = rot;
            var fwd = rot * Vector3.forward;
            float aimZ = 0f;
            float dist = 50f;
            for (int pass = 0; pass < 4; pass++)
            {
                float lo = 5f, hi = 300f;
                for (int i = 0; i < 40; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    transform.position = new Vector3(0f, 0f, aimZ) - fwd * mid;
                    if (FieldFits()) hi = mid; else lo = mid;
                }
                dist = hi;
                transform.position = new Vector3(0f, 0f, aimZ) - fwd * dist;
                // Balance the empty space above and below the field.
                float bottom = Cam.WorldToViewportPoint(new Vector3(0f, 0f, -Field.HalfHeight - Margin)).y;
                float top = Cam.WorldToViewportPoint(new Vector3(0f, 0f, Field.HalfHeight + Margin)).y;
                // >0: more empty space below the field than above, so aim further up the field.
                float imbalance = (bottom - (1f - top)) * 0.5f;
                aimZ += imbalance * Field.Height * 0.8f;
            }
            transform.position = new Vector3(0f, 0f, aimZ) - fwd * dist;
        }

        bool FieldFits()
        {
            float hw = Field.HalfWidth + Margin, hh = Field.HalfHeight + Margin;
            return Inside(new Vector3(-hw, 0f, -hh)) && Inside(new Vector3(hw, 0f, -hh)) &&
                   Inside(new Vector3(-hw, 0f, hh)) && Inside(new Vector3(hw, 0f, hh));
        }

        bool Inside(Vector3 world)
        {
            var v = Cam.WorldToViewportPoint(world);
            return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        }

        /// <summary>The play-field point under a screen position (pixels, origin bottom-left).</summary>
        public bool TryScreenToField(Vector2 screen, out Vec2 field)
        {
            var ray = Cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-5f) { field = Vec2.Zero; return false; }
            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0f) { field = Vec2.Zero; return false; }
            var p = ray.origin + ray.direction * t;
            field = new Vec2(p.x, p.z);
            return true;
        }

        /// <summary>Converts a finger movement in pixels into play-field units at that spot.</summary>
        public Vec2 ScreenDeltaToField(Vector2 screenPos, Vector2 screenDelta)
        {
            if (TryScreenToField(screenPos, out var a) && TryScreenToField(screenPos - screenDelta, out var b))
                return a - b;
            return Vec2.Zero;
        }
    }
}
