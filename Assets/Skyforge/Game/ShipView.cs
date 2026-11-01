using UnityEngine;
using Skyforge.Simulation;

namespace Skyforge.Game
{
    /// <summary>
    /// The player's ship on screen: follows the simulated position (interpolated), banks into turns,
    /// pulses the core and flickers the engines. Purely visual.
    /// </summary>
    public sealed class ShipView
    {
        const float MaxBank = 32f;
        const float MaxPitch = 8f;

        readonly VanguardModel.Parts _parts;
        float _bank, _pitch;

        public Transform Root => _parts.Root.transform;

        public ShipView(Transform parent, Loadout loadout)
        {
            _parts = VanguardModel.Build(parent, loadout);
        }

        /// <summary>Rebuild the weapon pods (after the loadout changes).</summary>
        public void RefitPods(Loadout loadout)
        {
            Object.Destroy(_parts.LeftPod.gameObject);
            Object.Destroy(_parts.RightPod.gameObject);
            _parts.LeftPod = VanguardModel.BuildPod(_parts.Body, Side.Left, loadout.LeftWing);
            _parts.RightPod = VanguardModel.BuildPod(_parts.Body, Side.Right, loadout.RightWing);
        }

        public void Update(Vec2 pos, Vec2 velocity, float maxSpeed, float dt, float time)
        {
            Root.localPosition = new Vector3(pos.X, 0f, pos.Y);

            // Bank into sideways movement, lean slightly with forward/back movement.
            float targetBank = -Mathf.Clamp(velocity.X / maxSpeed, -1f, 1f) * MaxBank;
            float targetPitch = -Mathf.Clamp(velocity.Y / maxSpeed, -1f, 1f) * MaxPitch;
            float k = 1f - Mathf.Exp(-dt * 10f);
            _bank = Mathf.Lerp(_bank, targetBank, k);
            _pitch = Mathf.Lerp(_pitch, targetPitch, k);
            _parts.Body.localRotation = Quaternion.Euler(_pitch, 0f, _bank);

            // Core: slow spin and breathing glow.
            float pulse = 1f + 0.08f * Mathf.Sin(time * 4f);
            _parts.Core.localRotation = Quaternion.Euler(0f, time * 90f, 0f);
            _parts.Core.localScale = Vector3.one * pulse;

            // Engines: flicker, and burn longer when flying forward.
            float thrust = 1f + 0.5f * Mathf.Clamp01(velocity.Y / maxSpeed) - 0.35f * Mathf.Clamp01(-velocity.Y / maxSpeed);
            for (int i = 0; i < _parts.Flames.Length; i++)
            {
                float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(time * 18f, i * 7.3f);
                _parts.Flames[i].localScale = new Vector3(1f, 1f, thrust * flicker);
            }
        }
    }
}
