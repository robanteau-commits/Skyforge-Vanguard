namespace Skyforge.Simulation
{
    /// <summary>A weapon fitted to a slot, at an upgrade level (1-based).</summary>
    public struct Fitted
    {
        public WeaponDef Weapon;
        public int Level;

        public Fitted(WeaponDef weapon, int level) { Weapon = weapon; Level = level; }

        public bool IsEmpty => Weapon == null;
        public WeaponLevel Stats => Weapon?.At(Level);
    }

    /// <summary>The ship's three hardpoints. Fixed for the whole stage.</summary>
    public sealed class Loadout
    {
        public Fitted LeftWing;
        public Fitted RightWing;
        public Fitted Core;

        public static Loadout Starter() => new Loadout
        {
            LeftWing = new Fitted(Weapons.PulseRepeater, 1),
            RightWing = new Fitted(Weapons.PulseRepeater, 1),
            Core = new Fitted(Weapons.PulseNova, 1),
        };

        public Fitted Wing(Side side) => side == Side.Left ? LeftWing : RightWing;
    }

    public enum Side : byte { Left, Right }
}
