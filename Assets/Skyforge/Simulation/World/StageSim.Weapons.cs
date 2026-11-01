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
                int n = ++Player.VolleyCount[(int)side];
                bool special = stats.SpecialEvery > 0 && n % stats.SpecialEvery == 0;
                FireVolley(fitted.Weapon.Kind, stats, Player.Pos + PlayerShip.PodOffset(side), side, 1f, special);
                cooldown += interval;
            }
            return cooldown;
        }

        /// <summary>One volley of a wing weapon from <paramref name="muzzle"/>, its damage scaled by <paramref name="factor"/>.
        /// <paramref name="special"/>: this is the level-5 special volley (charged bolt, giant spear).</summary>
        void FireVolley(WeaponKind kind, WeaponLevel st, Vec2 muzzle, Side side, float factor, bool special = false)
        {
            float dmg = st.Damage * factor;
            switch (kind)
            {
                case WeaponKind.PulseRepeater:
                {
                    // Parallel streams; from level 4 the outer ones fan slightly outwards.
                    for (int i = 0; i < st.Count; i++)
                    {
                        float k = i - (st.Count - 1) * 0.5f;
                        AddShot(muzzle + new Vec2(k * st.StreamGap, 0f), Vec2.FromHeading(k * st.AngleStep) * st.ProjectileSpeed, dmg, st.ProjectileRadius, side, ShotVisual.PulseBolt);
                    }
                    if (special)
                    {
                        var c = NewShot(muzzle, Vec2.Up * st.ProjectileSpeed, st.SpecialDamage * factor, st.ProjectileRadius * st.SpecialSize, side, ShotVisual.ChargedBolt);
                        c.Pierce = true;
                        Shots.Add(c);
                    }
                    break;
                }
                case WeaponKind.ScatterCannon:
                    for (int i = 0; i < st.Count; i++)
                    {
                        float a = st.Count == 1 ? 0f : -st.Spread * 0.5f + st.Spread * i / (st.Count - 1);
                        var s = NewShot(muzzle, Vec2.FromHeading(a) * st.ProjectileSpeed, dmg, st.ProjectileRadius, side, ShotVisual.Pellet);
                        s.Life = st.Life;
                        s.Burst = st.Burst;
                        Shots.Add(s);
                    }
                    break;
                case WeaponKind.LanceDriver:
                    for (int i = 0; i < st.Count; i++)
                    {
                        float k = i - (st.Count - 1) * 0.5f;
                        // Level 5: every third volley, the middle spear is a giant.
                        bool giant = special && i == st.Count / 2;
                        var s = NewShot(muzzle + new Vec2(k * st.StreamGap, 0f), Vec2.Up * st.ProjectileSpeed,
                            giant ? dmg * st.SpecialDamage : dmg, giant ? st.ProjectileRadius * st.SpecialSize : st.ProjectileRadius, side, ShotVisual.LanceBolt);
                        s.Pierce = true;
                        Shots.Add(s);
                    }
                    break;
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
                        s.Burst = st.Burst;
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
                        float a = baseAngle + (i - (st.Count - 1) * 0.5f) * st.Spread;
                        var s = NewShot(muzzle, Vec2.FromHeading(a) * st.ProjectileSpeed, dmg, st.ProjectileRadius, side, ShotVisual.Disc);
                        s.Bounce = true;
                        s.HitsLeft = st.MaxHits;
                        s.Life = st.Life;
                        s.Split = st.Split;
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
                    if (e.Gone || e.Shielded || !OnScreen(e) || _targets.Contains(e)) continue;
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
            foreach (var e in Enemies) if (e.Id == id && !e.Gone) return e;
            return null;
        }

        Enemy NearestEnemy(Vec2 from, float maxRange, List<int> exclude = null)
        {
            Enemy best = null;
            float bestD = maxRange * maxRange;
            foreach (var e in Enemies)
            {
                if (e.Gone || e.Shielded || !OnScreen(e) || (exclude != null && exclude.Contains(e.Id))) continue;
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
                    if (s.Life <= 0f)
                    {
                        Expire(s);
                        continue;
                    }
                }
                if (s.TurnRate > 0f) Steer(ref s);
                s.Pos += s.Vel * Field.Dt;
                if (s.Bounce)
                {
                    float edge = HalfWidth - s.Radius;
                    if (s.Pos.X < -edge && s.Vel.X < 0f) s.Vel = new Vec2(-s.Vel.X, s.Vel.Y);
                    else if (s.Pos.X > edge && s.Vel.X > 0f) s.Vel = new Vec2(-s.Vel.X, s.Vel.Y);
                }
                if (Outside(s.Pos, 1f)) continue;
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
                if (e.Gone) continue;
                if ((s.Pierce || s.HitsLeft > 0) && e.Id == s.LastHit) continue;
                float r = e.Radius + s.Radius;
                if (Vec2.DistanceSq(e.Pos, s.Pos) > r * r) continue;

                if (s.Blast > 0f)
                {
                    Blast(s.Pos, s.Blast, s.Damage);
                    if (s.Burst > 0) Bomblets(s);
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
                    if (s.HitsLeft == 0 && s.Split) SplitDisc(s);
                    return s.HitsLeft == 0;
                }
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ level-5 twists

        /// <summary>A projectile's life ran out: pellets burst into shrapnel, bomblets explode.</summary>
        void Expire(Shot s)
        {
            if (s.BlastOnExpire)
            {
                Blast(s.Pos, s.Blast, s.Damage);
                return;
            }
            if (s.Burst <= 0 || s.Visual != ShotVisual.Pellet) return;
            float heading = Heading(s.Vel);
            for (int i = 0; i < s.Burst; i++)
            {
                float a = heading - 50f + 100f * i / (s.Burst - 1);
                var shard = NewShot(s.Pos, Vec2.FromHeading(a) * 14f, s.Damage * 0.5f, 0.12f, s.Wing, ShotVisual.Shrapnel);
                shard.Life = 0.25f;
                Shots.Add(shard);
            }
        }

        /// <summary>Seeker level 5: bomblets thrown out from a missile's blast, each exploding a moment later.</summary>
        void Bomblets(Shot s)
        {
            float start = _rng.Range(0f, 360f);
            for (int i = 0; i < s.Burst; i++)
            {
                var b = NewShot(s.Pos, Vec2.FromHeading(start + 360f * i / s.Burst) * 6f, s.Damage * 0.4f, 0f, s.Wing, ShotVisual.Bomblet);
                b.Life = 0.28f;
                b.Blast = 0.6f;
                b.BlastOnExpire = true;
                Shots.Add(b);
            }
        }

        /// <summary>Ricochet level 5: a spent disc splits into two small ones flying off at angles.</summary>
        void SplitDisc(Shot s)
        {
            float heading = Heading(s.Vel);
            for (int i = -1; i <= 1; i += 2)
            {
                var d = NewShot(s.Pos, Vec2.FromHeading(heading + 35f * i) * s.Vel.Length, s.Damage * 0.6f, 0.22f, s.Wing, ShotVisual.Disc);
                d.Bounce = true;
                d.HitsLeft = 2;
                d.LastHit = s.LastHit;
                d.Life = 1.5f;
                Shots.Add(d);
            }
        }

        /// <summary>Damage every enemy touching a blast of <paramref name="radius"/> at <paramref name="at"/>.</summary>
        void Blast(Vec2 at, float radius, float damage)
        {
            Events.Add(new SimEvent { Type = SimEventType.Blast, Pos = at, Value = radius });
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Gone) continue;
                float r = radius + e.Radius;
                if (Vec2.DistanceSq(e.Pos, at) <= r * r) DamageEnemy(e, damage, at);
            }
        }

        // ------------------------------------------------------------------ Arc Coil

        /// <summary>Most strikes one Arc Coil volley can make (level 5's forks multiply quickly).</summary>
        public const int ArcMaxStrikes = 12;
        readonly List<Enemy> _arcFrontier = new List<Enemy>(8);
        readonly List<Enemy> _arcNext = new List<Enemy>(8);

        /// <summary>Strike the nearest enemy in range, then jump from enemy to enemy: one jump per
        /// level, each strike branching to <see cref="WeaponLevel.Fork"/> new targets.</summary>
        void FireArc(Vec2 from, WeaponLevel st, float damage)
        {
            var first = NearestEnemy(from, st.Range);
            if (first == null) return;
            _arcHit.Clear();
            _arcFrontier.Clear();
            Strike(from, first, damage);
            _arcFrontier.Add(first);
            int strikes = 1;
            for (int depth = 0; depth < st.Jumps && _arcFrontier.Count > 0 && strikes < ArcMaxStrikes; depth++)
            {
                _arcNext.Clear();
                foreach (var node in _arcFrontier)
                {
                    for (int b = 0; b < st.Fork && strikes < ArcMaxStrikes; b++)
                    {
                        var target = NearestEnemy(node.Pos, ArcJumpRange, _arcHit);
                        if (target == null) break;
                        Strike(node.Pos, target, damage);
                        _arcNext.Add(target);
                        strikes++;
                    }
                }
                _arcFrontier.Clear();
                _arcFrontier.AddRange(_arcNext);
            }
        }

        void Strike(Vec2 from, Enemy target, float damage)
        {
            var at = target.Pos;
            Events.Add(new SimEvent { Type = SimEventType.Arc, Pos = from, To = at, EnemyId = target.Id });
            _arcHit.Add(target.Id);
            DamageEnemy(target, damage, at);
        }

        // ------------------------------------------------------------------ Sunline Beam

        /// <summary>Seconds between the level-5 beam's full-power pulses.</summary>
        public const float BeamPulseInterval = 0.5f;

        /// <summary>
        /// The Sunline Beam: one strand per level step (1, 1, 2, 2, 3), each burning the first enemy
        /// in its column; from level 4 it carries on into the next enemy at half damage. Damage per
        /// second ramps from <see cref="WeaponLevel.Damage"/> to <see cref="WeaponLevel.DamageMax"/>
        /// while the beam stays on the same main target, and is shared between the strands.
        /// </summary>
        void UpdateBeam(Side side, Fitted f)
        {
            int w = (int)side;
            var p = Player;
            if (f.IsEmpty || f.Weapon.Kind != WeaponKind.SunlineBeam || !p.Alive)
            {
                p.BeamOn[w] = false;
                p.BeamStrands[w] = 0;
                p.BeamTarget[w] = 0;
                p.BeamTime[w] = 0f;
                return;
            }
            var st = f.Stats;
            var muzzle = p.Pos + PlayerShip.PodOffset(side);
            int n = Math.Min(st.Count, PlayerShip.MaxStrands);
            p.BeamOn[w] = true;
            p.BeamStrands[w] = n;

            // Find each strand's first (and, passing through, second) enemy; the main target is the
            // centre strand's, or failing that any strand's.
            Enemy main = null;
            for (int k = 0; k < n; k++)
            {
                int idx = w * PlayerShip.MaxStrands + k;
                float x = muzzle.X + (k - (n - 1) * 0.5f) * st.StreamGap;
                p.BeamStrandX[idx] = x - muzzle.X;
                var first = BeamHit(x, muzzle.Y, st.Width, null);
                _beamFirst[k] = first;
                _beamSecond[k] = first != null && st.PassThrough ? BeamHit(x, first.Pos.Y + 0.01f, st.Width, first) : null;
                var last = _beamSecond[k] ?? first;
                p.BeamEndY[idx] = last == null ? Field.HalfHeight + 1f : Math.Max(muzzle.Y, last.Pos.Y - last.Radius * 0.5f);
                if (first != null && (main == null || k == n / 2)) main = first;
            }

            if (main == null) { p.BeamTarget[w] = 0; p.BeamTime[w] = 0f; return; }
            if (main.Id == p.BeamTarget[w]) p.BeamTime[w] += Field.Dt;
            else { p.BeamTarget[w] = main.Id; p.BeamTime[w] = 0f; }
            float ramp = st.RampTime > 0f ? Math.Min(1f, p.BeamTime[w] / st.RampTime) : 1f;
            float dpsPerStrand = (st.Damage + (st.DamageMax - st.Damage) * ramp) / n;
            bool spark = Tick % BeamHitEventEvery == 0;
            for (int k = 0; k < n; k++)
            {
                var first = _beamFirst[k];
                if (first == null) continue;
                var second = _beamSecond[k];
                float x = muzzle.X + p.BeamStrandX[w * PlayerShip.MaxStrands + k];
                DamageEnemy(first, dpsPerStrand * Field.Dt, new Vec2(x, first.Pos.Y), spark);
                if (second != null) DamageEnemy(second, dpsPerStrand * 0.5f * Field.Dt, new Vec2(x, second.Pos.Y), spark);
            }

            // Level 5: at full power the beam pulses, hitting everything around the main target.
            if (st.PulseDamage > 0f && ramp >= 1f)
            {
                p.BeamPulseTimer[w] -= Field.Dt;
                if (p.BeamPulseTimer[w] <= 0f)
                {
                    p.BeamPulseTimer[w] = BeamPulseInterval;
                    var at = main.Pos;
                    Events.Add(new SimEvent { Type = SimEventType.BeamPulse, Pos = at, Value = st.PulseRadius });
                    for (int i = 0; i < Enemies.Count; i++)
                    {
                        var e = Enemies[i];
                        if (e.Gone) continue;
                        float r = st.PulseRadius + e.Radius;
                        if (Vec2.DistanceSq(e.Pos, at) <= r * r) DamageEnemy(e, st.PulseDamage, e.Pos);
                    }
                }
            }
            else
            {
                p.BeamPulseTimer[w] = BeamPulseInterval;
            }
        }

        readonly Enemy[] _beamFirst = new Enemy[PlayerShip.MaxStrands];
        readonly Enemy[] _beamSecond = new Enemy[PlayerShip.MaxStrands];

        /// <summary>The nearest enemy in a beam column at <paramref name="x"/>, above <paramref name="fromY"/>.</summary>
        Enemy BeamHit(float x, float fromY, float width, Enemy skip)
        {
            Enemy hit = null;
            float best = float.MaxValue;
            foreach (var e in Enemies)
            {
                if (e.Gone || e == skip || e.Pos.Y > Field.HalfHeight + 1f) continue;
                float dy = e.Pos.Y - fromY;
                if (dy < -e.Radius || Math.Abs(e.Pos.X - x) > width * 0.5f + e.Radius) continue;
                if (dy < best) { best = dy; hit = e; }
            }
            return hit;
        }
    }
}
