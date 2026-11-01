namespace Skyforge.Simulation
{
    public sealed partial class StageSim
    {
        void FireWings()
        {
            Player.LeftCooldown = FireWing(Side.Left, Loadout.LeftWing, Player.LeftCooldown);
            Player.RightCooldown = FireWing(Side.Right, Loadout.RightWing, Player.RightCooldown);
        }

        /// <summary>Counts the wing's cooldown down and fires as many volleys as are due.
        /// Returns the new cooldown. Leftover time carries over, so the fire rate is exact.</summary>
        float FireWing(Side side, Fitted fitted, float cooldown)
        {
            var stats = fitted.Stats;
            if (stats == null || stats.ShotsPerSecond <= 0f) return cooldown;
            cooldown -= Field.Dt;
            float interval = 1f / stats.ShotsPerSecond;
            while (cooldown <= 0f)
            {
                switch (fitted.Weapon.Kind)
                {
                    case WeaponKind.PulseRepeater: FirePulse(side, stats); break;
                }
                cooldown += interval;
            }
            return cooldown;
        }

        void FirePulse(Side side, WeaponLevel stats)
        {
            var muzzle = Player.Pos + PlayerShip.PodOffset(side);
            float first = -(stats.Streams - 1) * 0.5f * stats.StreamGap;
            for (int i = 0; i < stats.Streams; i++)
            {
                Shots.Add(new Shot
                {
                    Pos = muzzle + new Vec2(first + i * stats.StreamGap, 0f),
                    Vel = new Vec2(0f, stats.ProjectileSpeed),
                    Damage = stats.Damage,
                    Radius = stats.ProjectileRadius,
                    Wing = side,
                    Visual = ShotVisual.PulseBolt,
                });
            }
            Events.Add(new SimEvent { Type = SimEventType.Fired, Pos = muzzle, Wing = side });
        }

        /// <summary>Moves every shot, applies hits, and drops shots that hit or left the field.</summary>
        void MoveShots()
        {
            int write = 0;
            for (int i = 0; i < Shots.Count; i++)
            {
                var s = Shots[i];
                s.Pos += s.Vel * Field.Dt;
                if (Field.IsOutside(s.Pos, 1f)) continue;
                if (HitEnemy(ref s)) continue;
                Shots[write++] = s;
            }
            Shots.RemoveRange(write, Shots.Count - write);
        }

        /// <summary>True if the shot hit something and is used up.</summary>
        bool HitEnemy(ref Shot s)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Dead) continue;
                float r = e.Radius + s.Radius;
                if (Vec2.DistanceSq(e.Pos, s.Pos) > r * r) continue;
                DamageEnemy(e, s.Damage, s.Pos);
                return true;
            }
            return false;
        }
    }
}
