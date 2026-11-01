using System;

namespace Skyforge.Simulation
{
    /// <summary>
    /// Enemy fire, damage to the player (shield, hull, invulnerability), and pickups
    /// (docs/DESIGN.md §3.3 and §7).
    /// </summary>
    public sealed partial class StageSim
    {
        public const float EnemyShotDamage = 10f;
        public const float OrbRadius = 0.22f;
        public const float DartRadius = 0.18f;
        /// <summary>Hull repaired by a green orb.</summary>
        public const int RepairAmount = 15;
        /// <summary>Credits per gold shard, at most (a kill drops several shards when it's worth more).</summary>
        public const int CreditsPerShard = 5;
        public const float PickupCollectRadius = 0.9f;
        public const float PickupHomingSpeed = 20f;

        // ------------------------------------------------------------------ enemy fire

        Vec2 Muzzle(Enemy e) => e.Pos + Vec2.FromHeading(e.Facing) * (e.Radius * 0.6f);

        void FireShot(Vec2 from, Vec2 vel, EnemyShotVisual visual, float damage = EnemyShotDamage)
        {
            EnemyShots.Add(new EnemyShot
            {
                Pos = from,
                Vel = vel,
                Damage = damage,
                Radius = visual == EnemyShotVisual.Dart ? DartRadius : OrbRadius,
                Visual = visual,
            });
        }

        void FireAimed(Enemy e, float speed, EnemyShotVisual visual)
        {
            var from = Muzzle(e);
            FireShot(from, AimFrom(from) * speed, visual);
            Events.Add(new SimEvent { Type = SimEventType.EnemyFired, Pos = from, EnemyKind = e.Kind, EnemyId = e.Id });
        }

        /// <summary><paramref name="count"/> shots evenly around a circle, the first at <paramref name="offsetDeg"/>.</summary>
        void FireRing(Enemy e, int count, float speed, float offsetDeg, float damage = EnemyShotDamage)
        {
            for (int i = 0; i < count; i++)
                FireShot(e.Pos, Vec2.FromHeading(offsetDeg + 360f * i / count) * speed, EnemyShotVisual.Orb, damage);
            Events.Add(new SimEvent { Type = SimEventType.EnemyFired, Pos = e.Pos, EnemyKind = e.Kind, EnemyId = e.Id });
        }

        /// <summary><paramref name="count"/> shots fanned over <paramref name="spreadDeg"/>, centred on the player.</summary>
        void FireFan(Enemy e, int count, float spreadDeg, float speed)
        {
            var from = Muzzle(e);
            float centre = Heading(AimFrom(from));
            for (int i = 0; i < count; i++)
            {
                float a = count == 1 ? centre : centre - spreadDeg * 0.5f + spreadDeg * i / (count - 1);
                FireShot(from, Vec2.FromHeading(a) * speed, EnemyShotVisual.Dart);
            }
            Events.Add(new SimEvent { Type = SimEventType.EnemyFired, Pos = from, EnemyKind = e.Kind, EnemyId = e.Id });
        }

        void MineBurst(Enemy e)
        {
            FireRing(e, MineRingBullets, 6f, e.Phase * 57.29578f);
            Events.Add(new SimEvent { Type = SimEventType.MineBurst, Pos = e.Pos, EnemyKind = e.Kind, EnemyId = e.Id });
        }

        void MoveEnemyShots()
        {
            int write = 0;
            for (int i = 0; i < EnemyShots.Count; i++)
            {
                var s = EnemyShots[i];
                float slow = AegisUp && Vec2.DistanceSq(s.Pos, Player.Pos) < CoreStats.Range * CoreStats.Range ? CoreStats.Factor : 1f;
                s.Pos += s.Vel * (Field.Dt * slow);
                if (Outside(s.Pos, 1f)) continue;
                if (Player.Alive && (Player.Invulnerable <= 0f || AegisUp))
                {
                    float r = s.Radius + PlayerShip.HitRadius;
                    if (Vec2.DistanceSq(s.Pos, Player.Pos) <= r * r)
                    {
                        HurtPlayer(s.Damage);
                        continue; // used up; while invulnerable, bullets pass through
                    }
                }
                EnemyShots[write++] = s;
            }
            EnemyShots.RemoveRange(write, EnemyShots.Count - write);
        }

        // ------------------------------------------------------------------ player damage

        /// <summary>Shield cooldown and regeneration, invulnerability countdown.</summary>
        void UpdateDefences()
        {
            var p = Player;
            if (!p.Alive) return;
            if (p.Invulnerable > 0f) p.Invulnerable = Math.Max(0f, p.Invulnerable - Field.Dt);
            if (p.ShieldRegenWait > 0f) p.ShieldRegenWait = Math.Max(0f, p.ShieldRegenWait - Field.Dt);
            else if (p.Shield < p.MaxShield) p.Shield = Math.Min(p.MaxShield, p.Shield + p.ShieldRegenRate * Field.Dt);
        }

        /// <summary>
        /// Damage the player: the shield soaks it first, then the hull. Any hit gives a moment of
        /// invulnerability and restarts the shield's regeneration delay; hull damage also resets
        /// the combo. Returns false if the player couldn't be hurt (invulnerable or already gone).
        /// </summary>
        bool HurtPlayer(float damage)
        {
            var p = Player;
            if (!p.Alive || damage <= 0f) return false;
            if (AegisUp)
            {
                // The bubble absorbs it: the bullet is used up, nothing else happens.
                Events.Add(new SimEvent { Type = SimEventType.AegisBlock, Pos = p.Pos });
                return true;
            }
            if (p.Invulnerable > 0f) return false;
            p.Invulnerable = PlayerShip.HitInvulnerability;
            p.ShieldRegenWait = p.ShieldRegenDelay;
            float absorbed = Math.Min(p.Shield, damage);
            if (absorbed > 0f)
            {
                p.Shield -= absorbed;
                damage -= absorbed;
                Events.Add(new SimEvent { Type = SimEventType.ShieldHit, Pos = p.Pos, Value = absorbed });
            }
            if (damage > 0f)
            {
                p.Hull = Math.Max(0f, p.Hull - damage);
                HullDamageTaken += damage;
                ComboTenths = 10;
                _comboIdle = 0f;
                Events.Add(new SimEvent { Type = SimEventType.HullHit, Pos = p.Pos, Value = damage });
                if (p.Hull <= 0f)
                {
                    p.Alive = false;
                    p.Velocity = Vec2.Zero;
                    Events.Add(new SimEvent { Type = SimEventType.PlayerDestroyed, Pos = p.Pos });
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ pickups

        void AddPickup(PickupKind kind, Vec2 pos, Vec2 vel, int value)
        {
            Pickups.Add(new Pickup { Id = _nextPickupId++, Kind = kind, Pos = pos, Vel = vel, Value = value });
        }

        /// <summary>Gold shards for the enemy's credits, and maybe a repair orb.</summary>
        void DropLoot(Enemy e)
        {
            int credits = e.Def.Credits;
            if (credits > 0)
            {
                int shards = Math.Min(6, (credits + CreditsPerShard - 1) / CreditsPerShard);
                int each = credits / shards, extra = credits - each * shards;
                for (int i = 0; i < shards; i++)
                {
                    var dir = Vec2.FromHeading(_rng.Range(0f, 360f));
                    AddPickup(PickupKind.Credit, e.Pos, dir * _rng.Range(1.5f, 4f), each + (i == 0 ? extra : 0));
                }
            }
            if (e.Def.RepairChance > 0f && _rng.Next01() < e.Def.RepairChance)
                AddPickup(PickupKind.Repair, e.Pos, new Vec2(0f, 1.5f), RepairAmount);
        }

        void UpdatePickups()
        {
            var p = Player;
            int write = 0;
            for (int i = 0; i < Pickups.Count; i++)
            {
                var k = Pickups[i];
                if (p.Alive)
                {
                    var d = p.Pos - k.Pos;
                    // A cleared stage pulls everything in; otherwise the magnet catches what comes close.
                    if (!k.Homing && (Status == StageStatus.Cleared || d.LengthSq < p.MagnetRange * p.MagnetRange)) k.Homing = true;
                    if (k.Homing)
                    {
                        float dist = d.Length;
                        if (dist <= PickupCollectRadius || dist <= PickupHomingSpeed * Field.Dt)
                        {
                            Collect(k);
                            continue;
                        }
                        k.Vel = d / dist * PickupHomingSpeed;
                    }
                    else if (d.LengthSq < PickupCollectRadius * PickupCollectRadius)
                    {
                        Collect(k);
                        continue;
                    }
                }
                if (!k.Homing || !p.Alive)
                {
                    // Drift: settle to the scroll speed, falling down the screen.
                    var drift = new Vec2(0f, -Field.ScrollSpeed);
                    k.Vel = k.Vel + (drift - k.Vel) * Math.Min(1f, 2f * Field.Dt);
                }
                k.Pos += k.Vel * Field.Dt;
                if (k.Pos.Y < -Field.HalfHeight - 1f || Outside(k.Pos, 2f)) continue; // missed
                Pickups[write++] = k;
            }
            Pickups.RemoveRange(write, Pickups.Count - write);
        }

        void Collect(Pickup k)
        {
            if (k.Kind == PickupKind.Credit) Credits += k.Value;
            else Player.Hull = Math.Min(Player.MaxHull, Player.Hull + k.Value);
            Events.Add(new SimEvent { Type = SimEventType.Collected, Pos = k.Pos, EnemyId = k.Id, Value = k.Value, PickupKind = k.Kind });
        }
    }
}
