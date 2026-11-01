using UnityEngine;
using Skyforge.Simulation;
using R = Skyforge.Game.MeshBuilder.Ring;

namespace Skyforge.Game
{
    /// <summary>
    /// The player's ship: a sleek delta-wing fighter in play-field units (nose towards +Z,
    /// wingtips at X = ±1.6, a weapon pod under each wing at X = ±1.0). The pods change with the
    /// fitted weapon and its level; the core crystal glows in the spine.
    /// Each part uses its Blender model when there is one (docs/BLENDER.md: <c>vanguard</c>,
    /// <c>vanguard_core</c>, <c>vanguard_flame</c>, <c>pod_&lt;weaponId&gt;_lv&lt;N&gt;</c>) and
    /// otherwise the code-built version below.
    /// </summary>
    public static class VanguardModel
    {
        // Material slots of the body mesh.
        const int Hull = 0, Light = 1, Dark = 2, Canopy = 3, Glow = 4;

        public sealed class Parts
        {
            public GameObject Root;
            /// <summary>Banks and pitches; everything else hangs off it.</summary>
            public Transform Body;
            public Transform Core;
            public Transform[] Flames;
            public Transform LeftPod, RightPod;
        }

        // Mount points on the body (docs/BLENDER.md): each part's model origin goes here.
        public static readonly Vector3 CoreMount = new Vector3(0f, 0.27f, -0.35f);
        public static readonly Vector3 FlameMountRight = new Vector3(0.2f, 0.02f, -1.5f);
        public const float PodMountY = -0.13f;

        public static Parts Build(Transform parent, Loadout loadout)
        {
            var parts = new Parts { Root = new GameObject("Vanguard") };
            parts.Root.transform.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(parts.Root.transform, false);
            parts.Body = body;

            if (ModelLibrary.TryCreate("vanguard", body) == null)
            {
                var b = new MeshBuilder();
                BuildFuselage(b);
                BuildWing(b);
                b.Push(MeshBuilder.MirrorX);
                BuildWing(b);
                b.Pop();
                b.BuildObject("Airframe", body, Mats.Hull, Mats.HullLight, Mats.HullDark, Mats.Canopy, Mats.PlayerGlow);
            }

            // Core crystal, set into the spine behind the canopy.
            var coreGo = ModelLibrary.TryCreate("vanguard_core", body);
            if (coreGo == null)
            {
                var core = new MeshBuilder();
                core.Octahedron(Vector3.zero, new Vector3(0.13f, 0.19f, 0.22f), 0);
                coreGo = core.BuildObject("Core", body, Mats.PlayerGlow);
            }
            parts.Core = coreGo.transform;
            parts.Core.localPosition = CoreMount;

            // Engine flames: separate so they can flicker and stretch with speed (they scale along Z,
            // so a flame model points backwards from its origin).
            parts.Flames = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var flame = ModelLibrary.TryCreate("vanguard_flame", body);
                if (flame == null)
                {
                    var f = new MeshBuilder();
                    f.Octahedron(new Vector3(0f, 0f, -0.25f), new Vector3(0.09f, 0.09f, 0.3f), 0);
                    flame = f.BuildObject("Flame", body, Mats.PlayerGlow);
                }
                flame.name = i == 0 ? "FlameL" : "FlameR";
                var t = flame.transform;
                t.localPosition = new Vector3(i == 0 ? -FlameMountRight.x : FlameMountRight.x, FlameMountRight.y, FlameMountRight.z);
                parts.Flames[i] = t;
            }

            parts.LeftPod = BuildPod(body, Side.Left, loadout.LeftWing);
            parts.RightPod = BuildPod(body, Side.Right, loadout.RightWing);
            return parts;
        }

        static void BuildFuselage(MeshBuilder b)
        {
            // Main body: a hexagon with a spine on top, needle nose to flat tail.
            b.Loft(new[]
            {
                new R(1.75f, 0.0f, 0.0f, 0.02f),
                new R(1.15f, 0.2f, 0.13f, 0.03f),
                new R(0.35f, 0.38f, 0.22f, 0.05f),
                new R(-0.6f, 0.44f, 0.23f, 0.04f),
                new R(-1.2f, 0.36f, 0.2f, 0.03f),
                new R(-1.42f, 0.31f, 0.17f, 0.03f),
            }, 6, Hull, startDeg: 90f);

            // Canopy: dark glass bubble on the spine.
            b.Loft(new[]
            {
                new R(0.95f, 0.0f, 0.0f, 0.16f),
                new R(0.65f, 0.13f, 0.1f, 0.2f),
                new R(0.15f, 0.15f, 0.11f, 0.24f),
                new R(-0.2f, 0.05f, 0.04f, 0.25f),
            }, 6, Canopy, startDeg: 90f);

            // Dorsal plate around the core.
            b.ExtrudeXZ(new[]
            {
                new Vector2(0f, -0.05f), new Vector2(0.2f, -0.3f), new Vector2(0.17f, -0.7f),
                new Vector2(-0.17f, -0.7f), new Vector2(-0.2f, -0.3f),
            }, 0.2f, 0.29f, Light);

            // Engine nozzles with glowing exhaust rings.
            for (int s = -1; s <= 1; s += 2)
            {
                float x = 0.2f * s;
                b.Loft(new[]
                {
                    new R(-1.1f, 0.15f, 0.14f, 0.02f),
                    new R(-1.5f, 0.13f, 0.12f, 0.02f),
                }, 8, Dark, startDeg: 22.5f, x: x);
                b.Loft(new[]
                {
                    new R(-1.5f, 0.1f, 0.09f, 0.02f),
                    new R(-1.53f, 0.1f, 0.09f, 0.02f),
                }, 8, Glow, startDeg: 22.5f, x: x);
            }
        }

        /// <summary>Right wing, canard and tail fin. Mirrored for the left side.</summary>
        static void BuildWing(MeshBuilder b)
        {
            // Delta wing: thin slab from the fuselage to the tip.
            b.ExtrudeXZ(new[]
            {
                new Vector2(0.3f, 0.7f), new Vector2(1.6f, -0.82f), new Vector2(1.56f, -1.02f), new Vector2(0.35f, -0.98f),
            }, -0.03f, 0.04f, Hull, Dark);
            // Lighter armour panel on top.
            b.ExtrudeXZ(new[]
            {
                new Vector2(0.55f, 0.32f), new Vector2(1.32f, -0.6f), new Vector2(1.25f, -0.82f), new Vector2(0.58f, -0.82f),
            }, 0.04f, 0.06f, Light);
            // Glowing leading edge.
            b.Beam(new Vector3(0.42f, 0.05f, 0.55f), new Vector3(1.55f, 0.05f, -0.78f), 0.05f, 0.03f, Glow);
            // Wingtip fence.
            b.ExtrudeXZ(new[]
            {
                new Vector2(1.5f, -0.6f), new Vector2(1.62f, -0.82f), new Vector2(1.62f, -1.04f), new Vector2(1.5f, -1.04f),
            }, -0.08f, 0.12f, Dark);

            // Canard near the nose.
            b.ExtrudeXZ(new[]
            {
                new Vector2(0.16f, 1.0f), new Vector2(0.62f, 0.62f), new Vector2(0.58f, 0.52f), new Vector2(0.18f, 0.6f),
            }, 0.0f, 0.04f, Hull, Dark);

            // Tail fin, canted outwards.
            b.Push(Matrix4x4.TRS(new Vector3(0.33f, 0.18f, 0f), Quaternion.Euler(0f, 0f, -18f), Vector3.one));
            b.ExtrudeZY(new[]
            {
                new Vector2(-0.55f, 0f), new Vector2(-1.32f, 0f), new Vector2(-1.45f, 0.6f), new Vector2(-1.12f, 0.6f),
            }, 0.05f, Hull);
            b.Beam(new Vector3(0f, 0.6f, -1.13f), new Vector3(0f, 0.6f, -1.44f), 0.06f, 0.03f, Glow);
            b.Pop();
        }

        /// <summary>The weapon pod under one wing, shaped by the fitted weapon.</summary>
        public static Transform BuildPod(Transform body, Side side, Fitted fitted)
        {
            float x = PlayerShip.PodOffset(side).X;
            var model = fitted.IsEmpty ? null : PodModel(fitted, body);
            if (model != null)
            {
                model.name = side == Side.Left ? "PodL" : "PodR";
                model.transform.localPosition = new Vector3(x, PodMountY, 0f);
                // A left pod is the right pod mirrored, so asymmetric pods stay outboard.
                if (side == Side.Left) model.transform.localScale = new Vector3(-1f, 1f, 1f);
                return model.transform;
            }
            var b = new MeshBuilder();
            if (!fitted.IsEmpty)
            {
                switch (fitted.Weapon.Kind)
                {
                    case WeaponKind.PulseRepeater: PulseRepeaterPod(b, fitted.Stats.Count); break;
                    case WeaponKind.ScatterCannon: ScatterPod(b, fitted.Stats.Count); break;
                    case WeaponKind.LanceDriver: LancePod(b, fitted.Level); break;
                    case WeaponKind.SunlineBeam: SunlinePod(b, fitted.Level); break;
                    case WeaponKind.SeekerPods: SeekerPod(b, fitted.Stats.Count); break;
                    case WeaponKind.HornetRack: HornetPod(b, fitted.Stats.Count); break;
                    case WeaponKind.ArcCoil: ArcPod(b, fitted.Level); break;
                    case WeaponKind.RicochetDisc: RicochetPod(b, fitted.Stats.Count); break;
                }
            }
            var go = b.BuildObject(side == Side.Left ? "PodL" : "PodR", body,
                Mats.HullDark, Mats.HullLight, Mats.PlayerGlow);
            go.transform.localPosition = new Vector3(x, PodMountY, 0f);
            return go.transform;
        }

        /// <summary>The Blender pod for this weapon at this level: <c>pod_&lt;id&gt;_lv&lt;N&gt;</c>, else the
        /// nearest lower level, else <c>pod_&lt;id&gt;</c>; null if there's none.</summary>
        static GameObject PodModel(Fitted fitted, Transform body)
        {
            for (int lv = fitted.Level; lv >= 1; lv--)
            {
                var go = ModelLibrary.TryCreate("pod_" + fitted.Weapon.Id + "_lv" + lv, body);
                if (go != null) return go;
            }
            return ModelLibrary.TryCreate("pod_" + fitted.Weapon.Id, body);
        }

        /// <summary>Pulse Repeater: a slim pod with one barrel per stream and glowing muzzles.</summary>
        static void PulseRepeaterPod(MeshBuilder b, int barrels)
        {
            const int Dark = 0, Light = 1, Glow = 2;
            b.Loft(new[]
            {
                new R(0.3f, 0.08f, 0.07f),
                new R(0.15f, 0.13f, 0.11f),
                new R(-0.45f, 0.13f, 0.11f),
                new R(-0.7f, 0.06f, 0.05f),
            }, 6, Dark, startDeg: 90f);
            b.Box(new Vector3(0f, 0.11f, -0.15f), new Vector3(0.05f, 0.08f, 0.5f), Light); // pylon
            b.Beam(new Vector3(-0.13f, 0.02f, -0.1f), new Vector3(-0.13f, 0.02f, -0.4f), 0.02f, 0.03f, Glow);
            b.Beam(new Vector3(0.13f, 0.02f, -0.1f), new Vector3(0.13f, 0.02f, -0.4f), 0.02f, 0.03f, Glow);
            float gap = barrels > 1 ? 0.18f / (barrels - 1) : 0f;
            for (int i = 0; i < barrels; i++)
            {
                float bx = barrels > 1 ? -0.09f + i * gap : 0f;
                b.Box(new Vector3(bx, 0f, 0.43f), new Vector3(0.045f, 0.045f, 0.3f), Light);
                b.Box(new Vector3(bx, 0f, 0.6f), new Vector3(0.05f, 0.05f, 0.04f), Glow);
            }
        }

        // Pod material slots: 0 dark, 1 light, 2 glow. Muzzles sit at Z = +0.6 (where shots start).
        const int PDark = 0, PLight = 1, PGlow = 2;

        /// <summary>The common pod body and pylon; <paramref name="halfWidth"/> sets how chunky it is.</summary>
        static void PodBody(MeshBuilder b, float halfWidth, float front = 0.3f)
        {
            b.Loft(new[]
            {
                new R(front, halfWidth * 0.6f, halfWidth * 0.55f),
                new R(front - 0.15f, halfWidth, halfWidth * 0.85f),
                new R(-0.45f, halfWidth, halfWidth * 0.85f),
                new R(-0.7f, halfWidth * 0.45f, halfWidth * 0.4f),
            }, 6, PDark, startDeg: 90f);
            b.Box(new Vector3(0f, 0.11f, -0.15f), new Vector3(0.05f, 0.08f, 0.5f), PLight); // pylon
        }

        /// <summary>Scatter Cannon: a short, wide pod with a flared muzzle, one glowing bore per pellet.</summary>
        static void ScatterPod(MeshBuilder b, int pellets)
        {
            PodBody(b, 0.16f);
            b.ExtrudeXZ(new[] { new Vector2(-0.1f, 0.3f), new Vector2(0.1f, 0.3f), new Vector2(0.24f, 0.58f), new Vector2(-0.24f, 0.58f) }, -0.06f, 0.06f, PLight);
            for (int i = 0; i < pellets; i++)
            {
                float x = pellets == 1 ? 0f : -0.18f + 0.36f * i / (pellets - 1);
                b.Box(new Vector3(x, 0f, 0.6f), new Vector3(0.035f, 0.035f, 0.03f), PGlow);
            }
        }

        /// <summary>Lance Driver: a long rail gun; one glowing channel per spear (two rails each), thicker at level 2.</summary>
        static void LancePod(MeshBuilder b, int level)
        {
            PodBody(b, 0.12f + 0.02f * (level >= 3 ? 1 : 0));
            int spears = Simulation.Weapons.LanceDriver.At(level).Count;
            float gap = 0.13f;
            float channel = level == 2 ? 0.07f : 0.04f;
            for (int i = 0; i < spears; i++)
            {
                float x = (i - (spears - 1) * 0.5f) * gap;
                b.Box(new Vector3(x - channel, 0f, 0.3f), new Vector3(0.03f, 0.07f, 0.65f), PLight);
                b.Box(new Vector3(x + channel, 0f, 0.3f), new Vector3(0.03f, 0.07f, 0.65f), PLight);
                b.Box(new Vector3(x, 0f, 0.3f), new Vector3(channel * 1.4f, 0.02f, 0.6f), PGlow);
            }
        }

        /// <summary>Sunline Beam: one lens emitter per strand in a frame at the front.</summary>
        static void SunlinePod(MeshBuilder b, int level)
        {
            PodBody(b, 0.14f);
            int strands = Simulation.Weapons.SunlineBeam.At(level).Count;
            float r = level == 2 || level >= 4 ? 0.08f : 0.065f;
            b.Box(new Vector3(0f, 0f, 0.48f), new Vector3(0.12f + 0.12f * strands, r * 3f, 0.05f), PLight);
            for (int i = 0; i < strands; i++)
                b.Octahedron(new Vector3((i - (strands - 1) * 0.5f) * 0.12f, 0f, 0.55f), new Vector3(r, r, r * 1.3f), PGlow);
        }

        /// <summary>Seeker Pods: a boxy launcher with one tube per missile.</summary>
        static void SeekerPod(MeshBuilder b, int tubes)
        {
            PodBody(b, 0.15f);
            b.Box(new Vector3(0f, 0.02f, 0.38f), new Vector3(0.36f, 0.2f, 0.36f), PLight);
            for (int i = 0; i < tubes; i++)
            {
                float x = tubes == 1 ? 0f : -0.11f + 0.22f * i / (tubes - 1);
                b.Box(new Vector3(x, 0.02f, 0.57f), new Vector3(0.07f, 0.07f, 0.03f), PGlow);
            }
        }

        /// <summary>Hornet Rack: a wide block with a grid of micro-missile tubes.</summary>
        static void HornetPod(MeshBuilder b, int missiles)
        {
            PodBody(b, 0.15f);
            b.Box(new Vector3(0f, 0.02f, 0.36f), new Vector3(0.44f, 0.22f, 0.4f), PLight);
            int cols = (missiles + 1) / 2;
            for (int i = 0; i < missiles; i++)
            {
                int c = i % cols, r = i / cols;
                float x = cols == 1 ? 0f : -0.16f + 0.32f * c / (cols - 1);
                b.Box(new Vector3(x, -0.03f + r * 0.1f, 0.57f), new Vector3(0.045f, 0.045f, 0.03f), PGlow);
            }
        }

        /// <summary>Arc Coil: glowing coil rings along a spine, with two prongs at the front.</summary>
        static void ArcPod(MeshBuilder b, int level)
        {
            PodBody(b, 0.12f);
            int coils = 2 + (level + 1) / 2;
            for (int i = 0; i < coils; i++)
                b.Octahedron(new Vector3(0f, 0f, 0.1f - i * 0.16f), new Vector3(0.17f, 0.17f, 0.03f), PGlow);
            for (int s = -1; s <= 1; s += 2)
                b.Beam(new Vector3(s * 0.06f, 0f, 0.3f), new Vector3(s * 0.11f, 0f, 0.6f), 0.03f, 0.03f, PLight);
        }

        /// <summary>Ricochet Disc: a launcher with a glowing disc (two at higher levels) ready on top.</summary>
        static void RicochetPod(MeshBuilder b, int discs)
        {
            PodBody(b, 0.14f);
            for (int i = 0; i < discs; i++)
                b.Octahedron(new Vector3(0f, 0.13f + 0.07f * i, 0.32f - 0.12f * i), new Vector3(0.2f, 0.03f, 0.2f), PGlow);
        }
    }
}
