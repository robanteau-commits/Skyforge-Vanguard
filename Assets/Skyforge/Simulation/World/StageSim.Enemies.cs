using System;

namespace Skyforge.Simulation
{
    /// <summary>
    /// Enemy behaviour, per kind (docs/DESIGN.md §5). Every attack winds up for
    /// <see cref="TelegraphTime"/> first (<see cref="Enemy.Telegraph"/> rises 0→1, the view shows a
    /// muzzle glow) so the player always gets a warning.
    /// </summary>
    public sealed partial class StageSim
    {
        public const float TelegraphTime = 0.3f;
        /// <summary>Damage an enemy takes when it rams the player.</summary>
        public const float ContactSelfDamage = 50f;

        // Gnat
        public const float GnatShotChance = 0.35f;
        public const float GnatShotSpeed = 7f;
        // Needle
        public const float NeedleHoverTime = 0.9f;
        public const int NeedleBurst = 3;
        public const float NeedleShotSpeed = 9f;
        // Ram Drone
        public const float RamLockTime = 0.7f;
        public const float RamSpeed = 16f;
        // Spine Mine
        public const float MineTriggerRange = 2f;
        public const int MineRingBullets = 8;
        // Warden
        public const float WardenStayTime = 16f;
        public const float WardenVolleyInterval = 1.5f;
        // Bastion
        public const float BastionBurstInterval = 2.2f;
        public const int BastionBurst = 3;

        /// <summary>Kind-specific setup for an enemy the timeline just spawned.</summary>
        void InitEnemy(Enemy e, SpawnOrder o)
        {
            e.Path = o.Path;
            e.Param = o.Param;
            switch (e.Kind)
            {
                case EnemyKind.Gnat:
                    if (_rng.Next01() < GnatShotChance) e.ShotAt = _rng.Range(1f, 2.5f);
                    break;
                case EnemyKind.Needle:
                    e.Target = new Vec2(e.Pos.X, o.Param > 0f ? o.Param : 9f);
                    break;
                case EnemyKind.Warden:
                    e.Target = new Vec2(e.Pos.X, 10f);
                    e.Param = 0f;
                    break;
                case EnemyKind.Bastion:
                    e.Timer = 1f;
                    Events.Add(new SimEvent { Type = SimEventType.BastionSpawned, Pos = e.Pos, EnemyKind = e.Kind, EnemyId = e.Id });
                    break;
            }
        }

        void UpdateEnemies()
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Dead) continue;
                e.Age += Field.Dt;
                switch (e.Kind)
                {
                    case EnemyKind.PracticeDrone: MovePracticeDrone(e); break;
                    case EnemyKind.Gnat: UpdateGnat(e); break;
                    case EnemyKind.Needle: UpdateNeedle(e); break;
                    case EnemyKind.RamDrone: UpdateRam(e); break;
                    case EnemyKind.SpineMine: UpdateMine(e); break;
                    case EnemyKind.Warden: UpdateWarden(e); break;
                    case EnemyKind.Bastion: UpdateBastion(e); break;
                }
                if (!e.Dead) CheckContact(e);
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Degrees clockwise from up the screen.</summary>
        static float Heading(Vec2 v) => (float)(Math.Atan2(v.X, v.Y) * 180.0 / Math.PI);

        /// <summary>Unit vector from <paramref name="from"/> to the player (straight down if the ship is gone).</summary>
        Vec2 AimFrom(Vec2 from)
        {
            if (!Player.Alive) return new Vec2(0f, -1f);
            var d = (Player.Pos - from).Normalized;
            return d.LengthSq < 0.5f ? new Vec2(0f, -1f) : d;
        }

        /// <summary>Move along a path to <paramref name="p"/>, facing the direction of travel.</summary>
        static void MoveTo(Enemy e, Vec2 p)
        {
            e.Vel = (p - e.Pos) / Field.Dt;
            e.Pos = p;
            if (e.Vel.LengthSq > 0.04f) e.Facing = Heading(e.Vel);
        }

        bool OnScreen(Enemy e) =>
            e.Pos.X > -HalfWidth && e.Pos.X < HalfWidth && e.Pos.Y > -Field.HalfHeight && e.Pos.Y < Field.HalfHeight;

        /// <summary>Telegraph for an attack due in <paramref name="timeLeft"/> seconds.</summary>
        static float WindUp(float timeLeft) => timeLeft >= TelegraphTime ? 0f : timeLeft <= 0f ? 1f : 1f - timeLeft / TelegraphTime;

        static float Sin(float a) => (float)Math.Sin(a);
        static float Cos(float a) => (float)Math.Cos(a);

        // ------------------------------------------------------------------ Gnat

        void UpdateGnat(Enemy e)
        {
            float t = e.Age;
            Vec2 p;
            switch (e.Path)
            {
                case GnatPath.Weave:
                    p = new Vec2(e.BaseX + 2.6f * Sin(t * 2.2f), e.StartY - 5f * t);
                    break;
                case GnatPath.SweepRight:
                    p = new Vec2(-HalfWidth - 1.5f + 6.5f * XScale * t, e.StartY - 0.9f * t - 0.12f * t * t);
                    break;
                case GnatPath.SweepLeft:
                    p = new Vec2(HalfWidth + 1.5f - 6.5f * XScale * t, e.StartY - 0.9f * t - 0.12f * t * t);
                    break;
                case GnatPath.Swirl:
                    float a = t * 2.4f;
                    p = new Vec2(e.BaseX + 3.5f * Sin(a), e.StartY - 3f * t + 2.2f * (Cos(a) - 1f));
                    break;
                default: // Dive
                    p = new Vec2(e.BaseX, e.StartY - (e.Param > 0f ? e.Param : 6.5f) * t);
                    break;
            }
            MoveTo(e, p);

            if (e.ShotAt > 0f)
            {
                e.Telegraph = WindUp(e.ShotAt - t);
                if (t >= e.ShotAt)
                {
                    e.ShotAt = 0f;
                    e.Telegraph = 0f;
                    if (OnScreen(e) && Player.Alive && e.Pos.Y > Player.Pos.Y + 2f)
                        FireAimed(e, GnatShotSpeed, EnemyShotVisual.Orb);
                }
            }
        }

        // ------------------------------------------------------------------ Needle

        void UpdateNeedle(Enemy e)
        {
            switch (e.Mode)
            {
                case 0: // fly in to the hover point
                {
                    var p = e.Pos + (e.Target - e.Pos) * Math.Min(1f, 2.5f * Field.Dt);
                    MoveTo(e, p);
                    e.Facing = 180f;
                    if (Vec2.DistanceSq(e.Pos, e.Target) < 0.04f) { e.Mode = 1; e.Timer = NeedleHoverTime; }
                    break;
                }
                case 1: // hover, turn to the player, wind up
                    e.Vel = Vec2.Zero;
                    e.Timer -= Field.Dt;
                    e.Facing = Heading(AimFrom(e.Pos));
                    e.Telegraph = WindUp(e.Timer);
                    if (e.Timer <= 0f) { e.Mode = 2; e.Timer = 0f; e.Counter = 0; }
                    break;
                case 2: // 3-shot aimed burst
                    e.Timer -= Field.Dt;
                    e.Facing = Heading(AimFrom(e.Pos));
                    if (e.Timer <= 0f)
                    {
                        FireAimed(e, NeedleShotSpeed, EnemyShotVisual.Dart);
                        e.Counter++;
                        e.Timer = 0.12f;
                        if (e.Counter >= NeedleBurst) { e.Mode = 3; e.Timer = 0.5f; e.Telegraph = 0f; }
                    }
                    break;
                case 3: // pause
                    e.Timer -= Field.Dt;
                    if (e.Timer <= 0f) e.Mode = 4;
                    break;
                default: // dive off the bottom
                    e.Vel = new Vec2(0f, e.Vel.Y - 25f * Field.Dt);
                    e.Pos += e.Vel * Field.Dt;
                    e.Facing = 180f;
                    break;
            }
        }

        // ------------------------------------------------------------------ Ram Drone

        void UpdateRam(Enemy e)
        {
            if (e.Mode == 0)
            {
                // Drift in while the lock-on flash builds, then charge where the player is now.
                e.Vel = new Vec2(0f, -5f);
                e.Pos += e.Vel * Field.Dt;
                e.Timer += Field.Dt;
                e.Telegraph = Math.Min(1f, e.Timer / RamLockTime);
                e.Facing = Heading(AimFrom(e.Pos));
                if (e.Timer >= RamLockTime)
                {
                    var dir = AimFrom(e.Pos);
                    e.Vel = dir * RamSpeed;
                    e.Facing = Heading(dir);
                    e.Telegraph = 0f;
                    e.Mode = 1;
                }
            }
            else
            {
                e.Pos += e.Vel * Field.Dt;
            }
        }

        // ------------------------------------------------------------------ Spine Mine

        void UpdateMine(Enemy e)
        {
            e.Vel = new Vec2(0f, -Field.ScrollSpeed);
            e.Pos += e.Vel * Field.Dt;
            e.Facing += 40f * Field.Dt;
            if (Player.Alive && OnScreen(e) && Vec2.DistanceSq(e.Pos, Player.Pos) < MineTriggerRange * MineTriggerRange)
            {
                // Set off by proximity: it bursts, but it isn't a kill.
                e.Dead = true;
                MineBurst(e);
            }
        }

        // ------------------------------------------------------------------ Warden

        void UpdateWarden(Enemy e)
        {
            switch (e.Mode)
            {
                case 0: // slow approach to its station
                    MoveTo(e, e.Pos + (e.Target - e.Pos) * Math.Min(1f, 1.2f * Field.Dt));
                    e.Facing = 180f;
                    if (Vec2.DistanceSq(e.Pos, e.Target) < 0.09f) { e.Mode = 1; e.Timer = 1.2f; e.Counter = 0; }
                    break;
                case 1: // strafe slowly, alternating rotating rings and aimed fans
                {
                    e.Param += Field.Dt;
                    MoveTo(e, new Vec2(e.Target.X + 3f * Sin(e.Param * 0.5f), e.Target.Y + 0.6f * Sin(e.Param * 0.8f)));
                    e.Facing = 180f;
                    e.Timer -= Field.Dt;
                    e.Telegraph = WindUp(e.Timer);
                    if (e.Timer <= 0f)
                    {
                        if (e.Counter % 2 == 0) FireRing(e, 12, 5.5f, e.Counter * 11f, 8f);
                        else FireFan(e, 5, 40f, 7f);
                        e.Counter++;
                        e.Timer = WardenVolleyInterval;
                    }
                    if (e.Param >= WardenStayTime) { e.Mode = 2; e.Telegraph = 0f; }
                    break;
                }
                default: // withdraw upwards
                    MoveTo(e, e.Pos + new Vec2(0f, 4f * Field.Dt));
                    e.Facing = 180f;
                    break;
            }
        }

        // ------------------------------------------------------------------ Bastion

        void UpdateBastion(Enemy e)
        {
            e.Vel = new Vec2(0f, -Field.ScrollSpeed);
            e.Pos += e.Vel * Field.Dt;
            e.Facing = Heading(AimFrom(e.Pos));
            if (e.Pos.Y > Field.HalfHeight - 2f || e.Pos.Y < -Field.HalfHeight + 6f) { e.Telegraph = 0f; return; }
            e.Timer -= Field.Dt;
            if (e.Counter == 0) e.Telegraph = WindUp(e.Timer);
            if (e.Timer <= 0f)
            {
                FireAimed(e, 8f, EnemyShotVisual.Dart);
                e.Counter++;
                if (e.Counter >= BastionBurst) { e.Counter = 0; e.Timer = BastionBurstInterval; e.Telegraph = 0f; }
                else e.Timer = 0.15f;
            }
        }

        // ------------------------------------------------------------------ contact

        void CheckContact(Enemy e)
        {
            if (!Player.Alive || (Player.Invulnerable > 0f && !AegisUp)) return;
            float r = e.Radius + PlayerShip.BodyRadius;
            if (Vec2.DistanceSq(e.Pos, Player.Pos) > r * r) return;
            HurtPlayer(e.Def.ContactDamage);
            DamageEnemy(e, ContactSelfDamage, e.Pos);
        }
    }
}
