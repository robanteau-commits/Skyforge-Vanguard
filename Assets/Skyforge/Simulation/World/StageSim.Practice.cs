using System;

namespace Skyforge.Simulation
{
    /// <summary>
    /// Unarmed target drones for the M0 test flight: they drift down the screen in a lazy weave
    /// so there's something to shoot. Replaced by real stage waves in M1.
    /// </summary>
    public sealed partial class StageSim
    {
        public const int PracticeDroneMax = 7;
        public const float PracticeDroneInterval = 1.1f;
        public const float PracticeDroneHp = 12f;
        public const float PracticeDroneRadius = 0.75f;
        public const int PracticeDroneScore = 50;

        float _droneTimer = 1f;

        void UpdatePracticeDrones()
        {
            _droneTimer -= Field.Dt;
            if (_droneTimer > 0f) return;
            _droneTimer += PracticeDroneInterval;
            int alive = 0;
            foreach (var e in Enemies)
                if (e.Kind == EnemyKind.PracticeDrone) alive++;
            if (alive >= PracticeDroneMax) return;
            var pos = new Vec2(_rng.Range(-7f, 7f), Field.HalfHeight + 1.5f);
            SpawnEnemy(EnemyKind.PracticeDrone, pos, new Vec2(0f, -_rng.Range(3f, 4.5f)),
                PracticeDroneHp, PracticeDroneRadius, PracticeDroneScore);
        }

        static void MovePracticeDrone(Enemy e)
        {
            float y = e.Pos.Y + e.Vel.Y * Field.Dt;
            float x = e.BaseX + 1.5f * (float)Math.Sin(e.Age * 1.3f + e.Phase);
            e.Pos = new Vec2(x, y);
        }
    }
}
