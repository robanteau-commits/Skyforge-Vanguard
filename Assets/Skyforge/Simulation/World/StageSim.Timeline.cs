namespace Skyforge.Simulation
{
    /// <summary>The stage's spawn timeline and how a stage ends (cleared or failed).</summary>
    public sealed partial class StageSim
    {
        /// <summary>Seconds after the last spawn before a stage with no enemies left counts as cleared.</summary>
        public const float ClearGrace = 1f;

        int _nextOrder;

        /// <summary>0..1 through the stage's timeline (for the HUD).</summary>
        public float Progress => Stage == null || Stage.LastOrderTime <= 0f ? 0f : Time / Stage.LastOrderTime > 1f ? 1f : Time / Stage.LastOrderTime;

        void RunTimeline()
        {
            if (Stage == null) return;
            var orders = Stage.Orders;
            while (_nextOrder < orders.Length && orders[_nextOrder].Time <= Time)
            {
                var o = orders[_nextOrder++];
                if (o.Repair)
                {
                    AddPickup(PickupKind.Repair, new Vec2(o.X * XScale, o.Y), new Vec2(0f, -Field.ScrollSpeed), RepairAmount);
                    continue;
                }
                // Waves are authored for the portrait field: spread them across a wider one. Sweeps enter at the edges.
                float x = o.Kind == EnemyKind.Gnat && o.Path == GnatPath.SweepRight ? -HalfWidth - 1.5f
                        : o.Kind == EnemyKind.Gnat && o.Path == GnatPath.SweepLeft ? HalfWidth + 1.5f
                        : o.X * XScale;
                var e = SpawnEnemy(EnemyDefs.Of(o.Kind), new Vec2(x, o.Y), counted: true);
                e.BaseX = x;
                InitEnemy(e, o);
            }
        }

        void CheckStageEnd()
        {
            if (Status != StageStatus.Playing) return;
            if (!Player.Alive)
            {
                Status = StageStatus.Failed;
                return;
            }
            if (Stage == null || _nextOrder < Stage.Orders.Length) return;
            if (Enemies.Count > 0 || Time < Stage.LastOrderTime + ClearGrace) return;
            Status = StageStatus.Cleared;
            Events.Add(new SimEvent { Type = SimEventType.StageCleared, Pos = Player.Pos });
        }
    }
}
