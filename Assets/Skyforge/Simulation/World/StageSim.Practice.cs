using System;

namespace Skyforge.Simulation
{
    /// <summary>
    /// Unarmed target drones for the practice flight (no stage): they drift down the screen in a
    /// lazy weave so there's something to shoot.
    /// </summary>
    public sealed partial class StageSim
    {
        public const int PracticeDroneMax = 7;
        public const float PracticeDroneInterval = 1.1f;

        float _droneTimer = 1f;

        void UpdatePracticeDrones()
        {
            _droneTimer -= Field.Dt;
            if (_droneTimer > 0f) return;
            _droneTimer += PracticeDroneInterval;
            int alive = 0;
            foreach (var e in Enemies)
                if (e.Kind == EnemyKind.PracticeDrone && !e.Dead) alive++;
            if (alive >= PracticeDroneMax) return;
            var pos = new Vec2(_rng.Range(-7f, 7f), Field.HalfHeight + 1.5f);
            var d = SpawnEnemy(EnemyDefs.PracticeDrone, pos, counted: false);
            d.Vel = new Vec2(0f, -_rng.Range(3f, 4.5f));
        }

        static void MovePracticeDrone(Enemy e)
        {
            float y = e.Pos.Y + e.Vel.Y * Field.Dt;
            float x = e.BaseX + 1.5f * (float)Math.Sin(e.Age * 1.3f + e.Phase);
            e.Pos = new Vec2(x, y);
        }
    }
}
