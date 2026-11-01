using System;

namespace Skyforge.Simulation
{
    /// <summary>
    /// The Brood Carrier mini-boss and the Tidebreaker, World 1's boss (docs/DESIGN.md §5, §6.1).
    /// Both are built from parts: enemies with a <see cref="Enemy.ParentId"/> that ride along at an
    /// <see cref="Enemy.Offset"/>, can be destroyed on their own, and go down with their parent.
    /// The Tidebreaker's hull is <see cref="Enemy.Intangible"/>: it runs the fight (phases, Gnat
    /// waves, lasers) and falls when its core eye does.
    /// </summary>
    public sealed partial class StageSim
    {
        // Brood Carrier
        public const float CarrierSpeed = 1.1f;
        public const float CarrierVolleyInterval = 2.6f;
        public const float BayLaunchInterval = 3.2f;
        // Tidebreaker
        public const float BossStationY = 10.5f;
        public const float BossLungeY = 7.5f;
        public const float CannonOffsetX = 5.5f;
        public const float CannonStreamTime = 1.4f, CannonRestTime = 1.7f, CannonStreamGap = 0.11f;
        public const float MissileInterval = 4.2f, MissileSpeed = 3.6f, MissileTurn = 70f, MissileLife = 7f;
        public const float EyeSpiralTime = 3f, EyeSpiralRest = 1.3f, EyeSpiralGap = 0.16f;
        public const float EyeRingInterval = 1.8f;
        public const int EyeRingBullets = 30, EyeRingGap = 5;
        public const float BossGnatInterval = 7f;
        public const float LaserInterval = 6f, LaserWarnTime = 1.1f, LaserFireTime = 1.3f, LaserSweep = 32f;
        public const float LaserHalfWidth = 0.45f, LaserDamage = 20f;
        /// <summary>The eye's health fraction that starts phase 3.</summary>
        public const float BossPhase3At = 0.4f;

        /// <summary>The boss or mini-boss on screen, if any (the HUD shows its health bar).</summary>
        public Enemy Boss
        {
            get
            {
                foreach (var e in Enemies)
                    if (!e.Dead && (e.Kind == EnemyKind.Tidebreaker || e.Kind == EnemyKind.BroodCarrier)) return e;
                return null;
            }
        }

        /// <summary>The boss's health, 0..1: the Carrier's hull, or the Tidebreaker's parts together.</summary>
        public float BossHealth(Enemy boss) => boss == null || boss.MaxHp <= 0f ? 0f : Math.Max(0f, Math.Min(1f, boss.Hp / boss.MaxHp));

        Enemy SpawnPart(EnemyDef def, Enemy parent, Vec2 offset, bool shielded = false)
        {
            var e = SpawnEnemy(def, parent.Pos + offset, counted: false);
            e.ParentId = parent.Id;
            e.Offset = offset;
            e.Shielded = shielded;
            e.Facing = 180f;
            return e;
        }

        Enemy ParentOf(Enemy e)
        {
            foreach (var p in Enemies) if (p.Id == e.ParentId) return p;
            return null;
        }

        /// <summary>Keep a part on its parent. False if the parent is gone (the part goes too).</summary>
        bool Mount(Enemy e, out Enemy parent)
        {
            parent = ParentOf(e);
            if (parent == null || parent.Dead) { e.Dead = true; return false; }
            MoveTo(e, parent.Pos + e.Offset);
            return true;
        }

        /// <summary>A Gnat launched by a boss: not part of the stage's kill count.</summary>
        void LaunchGnat(Vec2 at, GnatPath path, float speed = 5.5f)
        {
            var g = SpawnEnemy(EnemyDefs.Gnat, at, counted: false);
            InitEnemy(g, new SpawnOrder { Kind = EnemyKind.Gnat, X = at.X, Y = at.Y, Path = path, Param = speed });
        }

        // ------------------------------------------------------------------ Brood Carrier

        void InitCarrier(Enemy e, SpawnOrder o)
        {
            // Enters from the side its X is on and crosses to the other.
            float dir = o.X <= 0f ? 1f : -1f;
            float y = o.Param > 0f ? o.Param : 10f;
            e.Pos = new Vec2(-dir * (HalfWidth + 3f), y);
            e.BaseX = e.Pos.X;
            e.StartY = y;
            e.Target = new Vec2(dir, 0f);
            e.Timer = 1.5f;
            SpawnPart(EnemyDefs.BroodBay, e, new Vec2(-1.9f, -0.2f)).Timer = 1f;
            SpawnPart(EnemyDefs.BroodBay, e, new Vec2(1.9f, -0.2f)).Timer = 1f + BayLaunchInterval * 0.5f;
        }

        void UpdateCarrier(Enemy e)
        {
            MoveTo(e, new Vec2(e.Pos.X + e.Target.X * CarrierSpeed * XScale * Field.Dt, e.StartY + 0.5f * Sin(e.Age * 0.7f)));
            e.Facing = 180f;
            if (Math.Abs(e.Pos.X) > HalfWidth - 1f) { e.Telegraph = 0f; return; }
            e.Timer -= Field.Dt;
            e.Telegraph = WindUp(e.Timer);
            if (e.Timer <= 0f)
            {
                FireFan(e, 7, 70f, 6.5f);
                e.Timer = CarrierVolleyInterval;
            }
        }

        void UpdateBay(Enemy e)
        {
            if (!Mount(e, out _)) return;
            e.Facing = 180f;
            if (Math.Abs(e.Pos.X) > HalfWidth - 0.5f) return;
            e.Timer -= Field.Dt;
            if (e.Timer <= 0f)
            {
                LaunchGnat(e.Pos + new Vec2(0f, -0.6f), GnatPath.Weave);
                e.Timer = BayLaunchInterval;
            }
        }

        // ------------------------------------------------------------------ Tidebreaker

        void InitTidebreaker(Enemy e)
        {
            e.Intangible = true;
            e.Pos = new Vec2(0f, Field.HalfHeight + 7f);
            e.BaseX = 0f;
            e.Target = new Vec2(0f, BossStationY);
            e.Mode = 0;
            var l = SpawnPart(EnemyDefs.TideCannon, e, new Vec2(-CannonOffsetX, -1.2f));
            var r = SpawnPart(EnemyDefs.TideCannon, e, new Vec2(CannonOffsetX, -1.2f));
            l.Timer = 0.8f;
            r.Timer = 0.8f + (CannonStreamTime + CannonRestTime) * 0.5f;
            SpawnPart(EnemyDefs.TideMissileBay, e, new Vec2(0f, 2.4f)).Timer = 2f;
            SpawnPart(EnemyDefs.TideEye, e, new Vec2(0f, -1.6f), shielded: true);
            // The boss bar shows the parts' health together.
            e.MaxHp = e.Hp = 0f;
            foreach (var p in Enemies) if (p.ParentId == e.Id) e.MaxHp += p.MaxHp;
            e.Hp = e.MaxHp;
        }

        /// <summary>The hull's live parts of one kind (and the first of them).</summary>
        int Parts(Enemy hull, EnemyKind kind, out Enemy first)
        {
            first = null;
            int n = 0;
            foreach (var p in Enemies)
            {
                if (p.ParentId != hull.Id || p.Kind != kind || p.Dead) continue;
                if (first == null) first = p;
                n++;
            }
            return n;
        }

        void UpdateTidebreaker(Enemy e)
        {
            int cannons = Parts(e, EnemyKind.TideCannon, out _);
            Parts(e, EnemyKind.TideEye, out var eye);
            if (eye == null)
            {
                // The eye is down: the fortress goes with it (its other parts and missiles too).
                for (int i = 0; i < Enemies.Count; i++)
                    if (Enemies[i].Kind == EnemyKind.TideMissile && !Enemies[i].Dead)
                    {
                        Enemies[i].Dead = true;
                        Events.Add(new SimEvent { Type = SimEventType.Blast, Pos = Enemies[i].Pos, Value = 0.8f });
                    }
                Lasers.Clear();
                Kill(e);
                return;
            }
            e.Hp = 0f;
            foreach (var p in Enemies) if (p.ParentId == e.Id && !p.Dead) e.Hp += Math.Max(0f, p.Hp);

            eye.Shielded = cannons > 0;
            int phase = e.Mode;
            if (e.Mode == 0 && Math.Abs(e.Pos.Y - e.Target.Y) < 0.1f) phase = 1;
            if (phase == 1 && cannons == 0) phase = 2;
            if (phase == 2 && eye.Hp < eye.MaxHp * BossPhase3At) phase = 3;
            if (phase != e.Mode)
            {
                e.Mode = phase;
                e.Timer = phase == 2 ? 2f : phase == 3 ? 2.5f : 0f;
                if (phase == 3) e.Target = new Vec2(0f, BossLungeY);
                // Give the player a beat before the eye opens up.
                eye.Mode = 0;
                eye.Timer = phase == 2 ? 1.5f : 1.2f;
                eye.Telegraph = 0f;
                Events.Add(new SimEvent { Type = SimEventType.BossPhase, Pos = e.Pos, EnemyKind = e.Kind, EnemyId = e.Id, Value = phase });
            }

            // Rise into place, then sway; phase 3 lunges forward.
            e.Param += Field.Dt;
            float y = e.Pos.Y + (e.Target.Y - e.Pos.Y) * Math.Min(1f, (e.Mode == 3 ? 1.5f : 0.8f) * Field.Dt);
            if (Math.Abs(y - e.Target.Y) < 0.1f) y = e.Target.Y;
            float x = e.Mode == 0 ? 0f : 1.2f * Sin(e.Param * 0.35f);
            MoveTo(e, new Vec2(x, y));
            e.Facing = 180f;

            if (e.Mode >= 2)
            {
                e.Timer -= Field.Dt;
                if (e.Timer <= 0f)
                {
                    if (e.Mode == 2)
                    {
                        // Gnat waves launch from the hull's sides.
                        for (int s = -1; s <= 1; s += 2)
                            for (int k = 0; k < 3; k++)
                                LaunchGnat(e.Pos + new Vec2(s * (3f + k * 0.9f), -1.5f - k * 0.6f), GnatPath.Swirl, 4f);
                        e.Timer = BossGnatInterval;
                    }
                    else
                    {
                        // Sweeping lasers from the cannon mounts, crossing towards the middle.
                        for (int s = -1; s <= 1; s += 2)
                            Lasers.Add(new HazardBeam
                            {
                                Origin = e.Pos + new Vec2(s * CannonOffsetX, -1.2f),
                                Heading = 180f + s * 28f,
                                Sweep = -s * LaserSweep,
                                Warn = LaserWarnTime,
                                Fire = LaserFireTime,
                            });
                        e.Timer = LaserInterval;
                    }
                }
            }
        }

        /// <summary>The boss hull a part is on, and its phase (0 while it's still rising).</summary>
        int BossPhaseOf(Enemy part, out Enemy hull) =>
            Mount(part, out hull) ? hull.Mode : -1;

        void UpdateTideCannon(Enemy e)
        {
            int phase = BossPhaseOf(e, out _);
            if (phase < 0) return;
            if (phase < 1) { e.Facing = 180f; return; }
            var aim = Heading(AimFrom(e.Pos));
            switch (e.Mode)
            {
                case 0: // wind up, tracking the player
                    e.Facing = aim;
                    e.Timer -= Field.Dt;
                    e.Telegraph = WindUp(e.Timer);
                    if (e.Timer <= 0f) { e.Mode = 1; e.Param = aim; e.Counter = 0; e.Timer = 0f; e.Telegraph = 0f; }
                    break;
                case 1: // a stream that sweeps across where the player was
                {
                    float t = e.Counter * CannonStreamGap / (Veteran ? VeteranDensity : 1f);
                    e.Facing = e.Param + 26f * Sin(t / CannonStreamTime * 6.2831853f);
                    e.Timer -= Field.Dt;
                    if (e.Timer <= 0f)
                    {
                        var from = Muzzle(e);
                        FireShot(from, Vec2.FromHeading(e.Facing) * 8f, EnemyShotVisual.Dart);
                        Events.Add(new SimEvent { Type = SimEventType.EnemyFired, Pos = from, EnemyKind = e.Kind, EnemyId = e.Id });
                        e.Counter++;
                        e.Timer = CannonStreamGap / (Veteran ? VeteranDensity : 1f);
                    }
                    if (t >= CannonStreamTime) { e.Mode = 2; e.Timer = CannonRestTime; }
                    break;
                }
                default: // rest
                    e.Facing = aim;
                    e.Timer -= Field.Dt;
                    if (e.Timer <= TelegraphTime) { e.Mode = 0; }
                    break;
            }
        }

        void UpdateTideMissileBay(Enemy e)
        {
            int phase = BossPhaseOf(e, out _);
            if (phase < 1) return;
            e.Facing = 180f;
            e.Timer -= Field.Dt;
            e.Telegraph = WindUp(e.Timer);
            if (e.Timer <= 0f)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    var m = SpawnEnemy(EnemyDefs.TideMissile, e.Pos + new Vec2(s * 0.9f, 0f), counted: false);
                    m.Facing = 180f + s * 50f;
                    m.Vel = Vec2.FromHeading(m.Facing) * MissileSpeed;
                }
                Events.Add(new SimEvent { Type = SimEventType.EnemyFired, Pos = e.Pos, EnemyKind = e.Kind, EnemyId = e.Id });
                e.Timer = MissileInterval;
                e.Telegraph = 0f;
            }
        }

        void UpdateTideMissile(Enemy e)
        {
            // Turns towards the player, slowly; pops when it runs out of fuel.
            float want = Heading(AimFrom(e.Pos));
            float diff = want - e.Facing;
            while (diff > 180f) diff -= 360f;
            while (diff < -180f) diff += 360f;
            float turn = MissileTurn * Field.Dt;
            e.Facing += diff > turn ? turn : diff < -turn ? -turn : diff;
            e.Vel = Vec2.FromHeading(e.Facing) * MissileSpeed * BulletSpeedScale;
            e.Pos += e.Vel * Field.Dt;
            if (e.Age >= MissileLife)
            {
                e.Dead = true;
                Events.Add(new SimEvent { Type = SimEventType.Blast, Pos = e.Pos, Value = 0.8f });
            }
        }

        void UpdateTideEye(Enemy e)
        {
            int phase = BossPhaseOf(e, out _);
            e.Facing = 180f; // it stares down the screen; it doesn't turn with the hull's sway
            if (phase < 2) { e.Telegraph = 0f; return; }
            if (phase == 2)
            {
                // Rotating spiral: three arms, turning, in bursts.
                switch (e.Mode)
                {
                    case 0: // rest, then wind up
                        e.Timer -= Field.Dt;
                        e.Telegraph = WindUp(e.Timer);
                        if (e.Timer <= 0f) { e.Mode = 1; e.Param = 0f; e.Telegraph = 0f; }
                        break;
                    default:
                        e.Param += Field.Dt;
                        e.Timer -= Field.Dt;
                        if (e.Timer <= 0f)
                        {
                            e.Counter++;
                            FireRing(e, 3, 5.5f, e.Counter * 13f);
                            e.Timer = EyeSpiralGap;
                        }
                        if (e.Param >= EyeSpiralTime) { e.Mode = 0; e.Timer = EyeSpiralRest; }
                        break;
                }
            }
            else
            {
                // Dense rings with a gap to slip through, aimed at a random side.
                e.Timer -= Field.Dt;
                e.Telegraph = WindUp(e.Timer);
                if (e.Timer <= 0f)
                {
                    int n = Dense(EyeRingBullets), gap = EyeRingGap;
                    int gapAt = _rng.Range(0, n);
                    float offset = _rng.Range(0f, 360f / n);
                    for (int i = 0; i < n; i++)
                    {
                        int k = (i - gapAt + n) % n;
                        if (k < gap) continue;
                        FireShot(e.Pos, Vec2.FromHeading(offset + 360f * i / n) * 5f, EnemyShotVisual.Orb);
                    }
                    Events.Add(new SimEvent { Type = SimEventType.EnemyFired, Pos = e.Pos, EnemyKind = e.Kind, EnemyId = e.Id });
                    e.Timer = EyeRingInterval;
                    e.Telegraph = 0f;
                }
            }
        }

        // ------------------------------------------------------------------ lasers

        void UpdateLasers()
        {
            int write = 0;
            for (int i = 0; i < Lasers.Count; i++)
            {
                var l = Lasers[i];
                if (l.Warn > 0f) l.Warn = Math.Max(0f, l.Warn - Field.Dt);
                else
                {
                    l.Heading += l.Sweep * Field.Dt;
                    l.Fire -= Field.Dt;
                    if (l.Fire <= 0f) continue;
                    if (Player.Alive && InLaser(l, Player.Pos, PlayerShip.HitRadius)) HurtPlayer(LaserDamage);
                }
                Lasers[write++] = l;
            }
            Lasers.RemoveRange(write, Lasers.Count - write);
        }

        /// <summary>Is a circle at <paramref name="p"/> touching the beam (a ray from its origin)?</summary>
        public static bool InLaser(HazardBeam l, Vec2 p, float radius)
        {
            var dir = Vec2.FromHeading(l.Heading);
            var d = p - l.Origin;
            float along = d.X * dir.X + d.Y * dir.Y;
            if (along < 0f) return false;
            float across = Math.Abs(d.X * dir.Y - d.Y * dir.X);
            return across <= LaserHalfWidth + radius;
        }
    }
}
