using System;

namespace CurtainCall.EscapeRoute
{
    /// <summary>All connected players assemble without a time limit. The waiting-room clock is independent.</summary>
    public sealed class EscapeAssemblyState
    {
        public enum Stage { Empty, Gathering, Departed }
        public Stage Status { get; private set; }
        public int Occupants { get; private set; }
        public int Eligible { get; private set; }
        public int Required { get; private set; }
        public bool CanDepart => Status == Stage.Gathering && Required > 0 && Eligible == Required;

        public void Tick(int occupants, int eligible, int required)
        {
            if (Status == Stage.Departed) return;
            Occupants = Math.Max(0, occupants);
            Eligible = Math.Clamp(eligible, 0, Occupants);
            Required = Math.Max(0, required);
            if (Occupants == 0) { Reset(); return; }
            Status = Stage.Gathering;
        }

        public bool Depart()
        {
            if (!CanDepart) return false;
            Status = Stage.Departed;
            return true;
        }

        public void Reset() { Status = Stage.Empty; Occupants = Eligible = Required = 0; }
    }
}
