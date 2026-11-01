using System;
using System.Collections.Generic;

namespace Skyforge.Simulation
{
    /// <summary>
    /// The player's wing weapons (docs/DESIGN.md §4.2): firing each kind, moving its projectiles
    /// (straight, homing, bouncing), hits (single, piercing, multi-hit, blast), the Sunline Beam and
    /// the Arc Coil's chain lightning. Wingmates reuse <see cref="FireVolley"/> from their own
    /// positions with a damage factor.
    /// </summary>
    public sealed partial class StageSim
    {
        /// <summary>How far an Arc Coil bolt jumps from one enemy to the next.</summary>
        public const float ArcJumpRange = 4.5f;
        /// <summary>Beams report a hit (for sparks) every this many steps.</summary>
        const int BeamHitEventEvery = 6;

        readonly List<Enemy> _targets = new List<Enemy>(16);
        readonly List<int> _arcHit = new List<int>(8);

        void FireWings()
        {
            Player.LeftCooldown = FireWing(Side.Left, Loadout.LeftWing, Player.LeftCooldown);
            Player.RightCooldown = FireWing(Side.Right, Loadout.RightWing, Player.RightCooldown);
            UpdateBeam(Side.Left, Loadout.LeftWing);
            UpdateBeam(Side.Right, Loadout.RightWing);
        }

        /// <summary>Counts the wing's cooldown down and fires as many volleys as are due.
        /// Returns the new cooldown. Leftover time carries over, so the fire rate is exact.</summary>
        float FireWing(Side side, Fitted fitted, float cooldown)
        {
            var stats = fitted.Stats;
            if (stats == null || fitted.Weapon.Kind == WeaponKind.SunlineBeam || stats.ShotsPerSecond <= 0f) return cooldown;
            cooldown -= Field.Dt;
            float interval = 1f / stats.ShotsPerSecond;
            while (cooldown <= 0f)
            {
                FireVolley(fitted.Weapon.Kind, stats, Player.Pos + PlayerShip.PodOffset(side), side, 1f);
                cooldown += interval;
            }
            return cooldown;
        }

        /// <summary>One volley of a wing weapon from <paramref name="muzzle"/>, its damage scaled by <paramref name="factor"/>.</summary>
        void FireVolley(WeaponKind kind, WeaponLevel st, Vec2 muzzle, Side side, float factor)
        {
            float dmg = st.Damage * factor;
            switch (kind)
            {
                case WeaponKind.PulseRepeater:
                {
                    float first = -(st.Count - 1) * 0.5f * st.StreamGap;
                    for (int i = 0; i < st.Count; i++)
                        AddShot(muzzle + new Vec2(first + i * st.StreamGap, 0f), Vec2.Up * st.ProjectileSpeed, dmg, st.ProjectileRadius, side, ShotVisual.PulseBolt);
                    break;
                }
                case WeaponKind.ScatterCannon:
                    for (int i = 0; i < st.Count; i++)
                    {
                        float a = st.Count == 1 ? 0f : -st.Spread * 0.5f + st.Spread * i / (st.Count - 1);
                        var s = NewShot(muzzle, Vec2.FromHeading(a) * st.ProjectileSpeed, dmg, st.ProjectileRadius, side, ShotVisual.Pellet);
                        s.Life = st.Life;
                        Shots.Add(s);
                    }
                    break;
                case WeaponKind.LanceDriver:
                {
                    var s = NewShot(muzzle, Vec2.Up * st.ProjectileSpeed, dmg, st.ProjectileRadius, side, ShotVisual.LanceBolt);
                    s.Pierce = true;
                    Shots.Add(s);
                    break;
                }
                case WeaponKind.SeekerPods:
                case WeaponKind.HornetRack:
                {
                    bool hornet = kind == WeaponKind.HornetRack;
                    PickTargets(muzzle, st.Count);
                    // Hornets fan out wide (leaning outboard), Seekers launch nearly straight.
                    float lean = hornet ? (side == Side.Left ? -20f : 20f) : 0f;
                    for (int i = 0; i < st.Count; i++)
                    {
                        float a = lean + (st.Count == 1 ? 0f : -st.Spread * 0.5f + st.Spread * i / (st.Count - 1));
                        var s = NewShot(muzzle, Vec2.FromHeading(a) * st.ProjectileSpeed, dmg, st.ProjectileRadius, side,
                            hornet ? ShotVisual.MicroMissile : ShotVisual.Missile);
                        s.TurnRate = st.TurnRate;
                        s.Blast = st.Blast;
                        s.Life = st.Life;
                        s.TargetId = _targets.Count == 0 ? 0 : _targets[i % _targets.Count].Id;
                        Shots.Add(s);
                    }
                    break;
                }
                case WeaponKind.ArcCoil:
                    FireArc(muzzle, st, dmg);
                    break;
                case WeaponKind.RicochetDisc:
                {
                    float baseAngle = side == Side.Left ? -20f : 20f;
                    for (int i = 0; i < st.Count; i++)
                    {
                        float a = st.Count == 1 ? baseAngle : baseAngle + (i == 0 ? -12f : 12f);
                        var s = NewShot(muzzle, Vec2.FromHeading(a) * st.ProjectileSpeed, dmg, st.ProjectileRadius, side, ShotVisual.Disc);
                        s.Bounce = true;
                        s.HitsLeft = st.MaxHits;
                        s.Life = st.Life;
                        Shots.Add(s);
                    }
                    break;
                }
                case WeaponKind.SunlineBeam:
                    // Only wingmates fire a beam weapon as a volley: bolts worth the beam's average damage.
                    AddShot(muzzle, Vec2.Up * 34f, (st.Damage + st.DamageMax) * 0.5f / WingmateBeamRate * factor, 0.2f, side, ShotVisual.PulseBolt);
                    break;
            }
            Events.Add(new SimEvent { Type = SimEventType.Fired, Pos = muzzle, Wing = side });
        }

        static Shot NewShot(Vec2 pos, Vec2 vel, float dmg, float radius, Side side, ShotVisual visual) =>
            new Shot { Pos = pos, Vel = vel, Damage = dmg, Radius = radius, Wing = side, Visual = visual };

        void AddShot(Vec2 pos, Vec2 vel, float dmg, float radius, Side side, ShotVisual visual) =>
            Shots.Add(NewShot(pos, vel, dmg, radius, side, visual));

        // ------------------------------------------------------------------ targeting

        /// <summary>Fills <see cref="_targets"/> with up to <paramref name="count"/> live enemies, nearest first.</summary>
        void PickTargets(Vec2 from, int count)
        {
            _targets.Clear();
            for (int k = 0; k < count; k++)
            {
                Enemy best = null;
                float bestD = float.MaxValue;
                foreach (var e in Enemies)
                {
                    if (e.Dead || !OnScreen(e) || _targets.Contains(e)) continue;
                    float d = Vec2.DistanceSq(e.Pos, from);
                    if (d < bestD) { bestD = d; best = e; }
                }
                if (best == null) break;
                _targets.Add(best);
            }
        }

        Enemy EnemyById(int id)
        {
            if (id == 0) return null;
            foreach (var e in Enemies) if (e.Id == id && !e.Dead) return e;
            return null;
        }

        Enemy NearestEnemy(Vec2 from, float maxRange, List<int> exclude = null)
        {
            Enemy best = null;
            float bestD = maxRange * maxRange;
            foreach (var e in Enemies)
            {
                if (e.Dead || !OnScreen(e) || (exclude != null && exclude.Contains(e.Id))) continue;
                float d = Vec2.DistanceSq(e.Pos, from);
                if (d <= bestD) { bestD = d; best = e; }
            }
            return best;
        }

        // ------------------------------------------------------------------ projectiles

        /// <summary>Moves every shot (homing, bouncing), applies hits, and drops spent shots.</summary>
        void MoveShots()
        {
            int write = 0;
            for (int i = 0; i < Shots.Count; i++)
            {
                var s = Shots[i];
                if (s.Life > 0f)
                {
                    s.Life -= Field.Dt;
                    if (s.Life <= 0f) continue;
                }
                if (s.TurnRate > 0f) Steer(ref s);
                s.Pos += s.Vel * Field.Dt;
                if (s.Bounce)
                {
                    float edge = Field.HalfWidth - s.Radius;
                    if (s.Pos.X < -edge && s.Vel.X < 0f) s.Vel = new Vec2(-s.Vel.X, s.Vel.Y);
                    else if (s.Pos.X > edge && s.Vel.X > 0f) s.Vel = new Vec2(-s.Vel.X, s.Vel.Y);
                }
                if (Field.IsOutside(s.Pos, 1f)) continue;
                if (HitEnemy(ref s)) continue;
                Shots[write++] = s;
            }
            Shots.RemoveRange(write, Shots.Count - write);
        }

        /// <summary>Turn a homing shot towards its target (picking a new one if it's gone).</summary>
        void Steer(ref Shot s)
        {
            var t = EnemyById(s.TargetId);
            if (t == null)
            {
                t = NearestEnemy(s.Pos, 40f);
                s.TargetId = t?.Id ?? 0;
                if (t == null) return;
            }
            float speed = s.Vel.Length;
            float heading = Heading(s.Vel);
            float want = Heading(t.Pos - s.Pos);
            float delta = want - heading;
            while (delta > 180f) delta -= 360f;
            while (delta < -180f) delta += 360f;
            float max = s.TurnRate * Field.Dt;
            if (delta > max) delta = max; else if (delta < -max) delta = -max;
            s.Vel = Vec2.FromHeading(heading + delta) * speed;
        }

        /// <summary>True if the shot hit something and is used up.</summary>
        bool HitEnemy(ref Shot s)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Dead) continue;
                if ((s.Pierce || s.HitsLeft > 0) && e.Id == s.LastHit) continue;
                float r = e.Radius + s.Radius;
                if (Vec2.DistanceSq(e.Pos, s.Pos) > r * r) continue;

                if (s.Blast > 0f)
                {
                    Blast(s.Pos, s.Blast, s.Damage);
                    return true;
                }
                DamageEnemy(e, s.Damage, s.Pos);
                if (s.Pierce)
                {
                    s.LastHit = e.Id;
                    return false;
                }
                if (s.HitsLeft > 0)
                {
                    s.HitsLeft--;
                    s.LastHit = e.Id;
                    return s.HitsLeft == 0;
                }
                return true;
            }
            return false;
        }

        /// <summary>Damage every enemy touching a blast of <paramref name="radius"/> at <paramref name="at"/>.</summary>
        void Blast(Vec2 at, float radius, float damage)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Dead) continue;
                float r = radius + e.Radius;
                if (Vec2.DistanceSq(e.Pos, at) <= r * r) DamageEnemy(e, damage, at);
            }
        }

        // ------------------------------------------------------------------ Arc Coil

        /// <summary>Strike the nearest enemy in range, then jump from enemy to enemy.</summary>
        void FireArc(Vec2 from, WeaponLevel st, float damage)
        {
            var target = NearestEnemy(from, st.Range);
            if (target == null) return;
            _arcHit.Clear();
            var prev = from;
            int jumps = st.Jumps;
            while (target != null)
            {
                var at = target.Pos;
                Events.Add(new SimEvent { Type = SimEventType.Arc, Pos = prev, To = at, EnemyId = target.Id });
                _arcHit.Add(target.Id);
                DamageEnemy(target, damage, at);
                if (jumps-- <= 0) break;
                prev = at;
                target = NearestEnemy(at, ArcJumpRange, _arcHit);
            }
        }

        // ------------------------------------------------------------------ Sunline Beam

        void UpdateBeam(Side side, Fitted f)
        {
            int i = (int)side;
            var p = Player;
            if (f.IsEmpty || f.Weapon.Kind != WeaponKind.SunlineBeam || !p.Alive)
            {
                p.BeamOn[i] = false;
                p.BeamTarget[i] = 0;
                p.BeamTime[i] = 0f;
                return;
            }
            var st = f.Stats;
            var muzzle = p.Pos + PlayerShip.PodOffset(side);
            Enemy hit = null;
            float best = float.MaxValue;
            foreach (var e in Enemies)
            {
                if (e.Dead || e.Pos.Y > Field.HalfHeight + 1f) continue;
                float dy = e.Pos.Y - muzzle.Y;
                if (dy < -e.Radius || Math.Abs(e.Pos.X - muzzle.X) > st.Width * 0.5f + e.Radius) continue;
                if (dy < best) { best = dy; hit = e; }
            }
            p.BeamOn[i] = true;
            if (hit == null)
            {
                p.BeamEndY[i] = Field.HalfHeight + 1f;
                p.BeamTarget[i] = 0;
                p.BeamTime[i] = 0f;
                return;
            }
            if (hit.Id == p.BeamTarget[i]) p.BeamTime[i] += Field.Dt;
            else { p.BeamTarget[i] = hit.Id; p.BeamTime[i] = 0f; }
            float ramp = st.RampTime > 0f ? Math.Min(1f, p.BeamTime[i] / st.RampTime) : 1f;
            float dps = st.Damage + (st.DamageMax - st.Damage) * ramp;
            p.BeamEndY[i] = Math.Max(muzzle.Y, hit.Pos.Y - hit.Radius * 0.5f);
            DamageEnemy(hit, dps * Field.Dt, new Vec2(muzzle.X, p.BeamEndY[i]), Tick % BeamHitEventEvery == 0);
        }
    }
}
