using System;

namespace Skyforge.Simulation
{
    /// <summary>
    /// The core special (docs/DESIGN.md §4.3). The core charges from 0 to 100: +2 per second of
    /// flight, +1 per kill (+5 for gunship-class enemies), times the Capacitor stat. When it's full
    /// the Core button fires it:
    /// <list type="bullet">
    /// <item>Pulse Nova: clears all enemy bullets, damages every enemy on screen, brief invulnerability.</item>
    /// <item>Solar Lance: a wide beam ahead of the ship that burns enemies and cancels bullets.</item>
    /// <item>Aegis Field: absorbs every hit; enemy bullets near the ship slow down.</item>
    /// <item>Wingmates: drones beside the ship that copy its wing weapons at reduced damage.</item>
    /// </list>
    /// It doesn't charge while a lasting effect is running.
    /// </summary>
    public sealed partial class StageSim
    {
        public const float CoreChargePerSecond = 2f;
        public const float CoreChargePerKill = 1f;
        public const float CoreChargePerBigKill = 5f;
        /// <summary>Wingmates carrying a beam weapon fire bolts at this rate instead.</summary>
        public const float WingmateBeamRate = 10f;

        /// <summary>The fitted core, or null.</summary>
        public WeaponDef CoreWeapon => Loadout.Core.Weapon;
        public WeaponLevel CoreStats => Loadout.Core.Stats;

        bool CoreRunning(WeaponKind kind) => Player.CoreActive && CoreWeapon != null && CoreWeapon.Kind == kind;
        /// <summary>The Aegis Field is up: hits are absorbed and nearby bullets slow down.</summary>
        public bool AegisUp => CoreRunning(WeaponKind.AegisField);
        public bool SolarLanceUp => CoreRunning(WeaponKind.SolarLance);

        void ChargeCore(float amount)
        {
            var p = Player;
            if (p.CoreActive || CoreWeapon == null) return;
            p.CoreCharge = Math.Min(PlayerShip.CoreChargeMax, p.CoreCharge + amount * p.Capacitor * Setup.CoreChargeMultiplier);
        }

        void UpdateCore(PlayerInput input)
        {
            var p = Player;
            if (!p.Alive)
            {
                if (p.CoreActive) EndCore();
                return;
            }
            if (p.CoreActive)
            {
                p.CoreTimer -= Field.Dt;
                RunCore();
                if (p.CoreTimer <= 0f) EndCore();
            }
            else
            {
                ChargeCore(CoreChargePerSecond * Field.Dt);
            }
            if (input.Core && p.CoreReady && !p.CoreActive && CoreWeapon != null && Status == StageStatus.Playing)
                ActivateCore();
        }

        void ActivateCore()
        {
            var p = Player;
            var st = CoreStats;
            var kind = CoreWeapon.Kind;
            p.CoreCharge = 0f;
            Events.Add(new SimEvent { Type = SimEventType.CoreActivated, Pos = p.Pos, EnemyId = (int)kind });
            switch (kind)
            {
                case WeaponKind.PulseNova:
                    EnemyShots.Clear();
                    for (int i = 0; i < Enemies.Count; i++)
                    {
                        var e = Enemies[i];
                        if (!e.Gone && OnScreen(e)) DamageEnemy(e, st.Damage, e.Pos);
                    }
                    p.Invulnerable = Math.Max(p.Invulnerable, st.Duration);
                    break;
                case WeaponKind.SolarLance:
                case WeaponKind.AegisField:
                case WeaponKind.Wingmates:
                    p.CoreActive = true;
                    p.CoreTimer = st.Duration;
                    if (kind == WeaponKind.Wingmates)
                    {
                        p.WingmateCount = Math.Min(st.Count, p.WingmateCooldown.Length);
                        for (int i = 0; i < p.WingmateCooldown.Length; i++) p.WingmateCooldown[i] = 0.1f * i;
                    }
                    break;
            }
        }

        void RunCore()
        {
            switch (CoreWeapon.Kind)
            {
                case WeaponKind.SolarLance: RunSolarLance(CoreStats); break;
                case WeaponKind.Wingmates: RunWingmates(CoreStats); break;
                // Aegis Field works through HurtPlayer and MoveEnemyShots.
            }
        }

        void EndCore()
        {
            var p = Player;
            p.CoreActive = false;
            p.CoreTimer = 0f;
            p.WingmateCount = 0;
            Events.Add(new SimEvent { Type = SimEventType.CoreEnded, Pos = p.Pos });
        }

        /// <summary>The Solar Lance's beam: everything ahead of the ship within half its width.</summary>
        bool InLance(Vec2 pos, float radius) =>
            pos.Y > Player.Pos.Y - radius && Math.Abs(pos.X - Player.Pos.X) <= CoreStats.Width * 0.5f + radius;

        void RunSolarLance(WeaponLevel st)
        {
            bool spark = Tick % BeamHitEventEvery == 0;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (!e.Gone && OnScreen(e) && InLance(e.Pos, e.Radius))
                    DamageEnemy(e, st.Damage * Field.Dt, e.Pos, spark);
            }
            EnemyShots.RemoveAll(s => InLance(s.Pos, s.Radius));
        }

        void RunWingmates(WeaponLevel st)
        {
            var p = Player;
            for (int i = 0; i < p.WingmateCount; i++)
            {
                // Drone 0 copies the left wing, drone 1 the right, drone 2 alternates.
                var side = i == 0 ? Side.Left : i == 1 ? Side.Right : (Tick / 30) % 2 == 0 ? Side.Left : Side.Right;
                var wing = Loadout.Wing(side);
                if (wing.IsEmpty) wing = Loadout.Wing(side == Side.Left ? Side.Right : Side.Left);
                if (wing.IsEmpty) continue;
                var ws = wing.Stats;
                float rate = wing.Weapon.Kind == WeaponKind.SunlineBeam ? WingmateBeamRate : ws.ShotsPerSecond;
                if (rate <= 0f) continue;
                p.WingmateCooldown[i] -= Field.Dt;
                while (p.WingmateCooldown[i] <= 0f)
                {
                    FireVolley(wing.Weapon.Kind, ws, p.Pos + PlayerShip.WingmateOffset(i) + new Vec2(0f, 0.5f), side, st.Factor);
                    p.WingmateCooldown[i] += 1f / rate;
                }
            }
        }
    }
}
