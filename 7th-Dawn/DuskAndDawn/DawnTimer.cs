using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class DawnTimer
    {
        public int MaxBudget { get; }
        public int RemainingBudget { get; private set; }

        public bool HasTimeRemaining => RemainingBudget > 0;

        public DawnTimer(int startingBudget)
        {
            MaxBudget = startingBudget;
            RemainingBudget = startingBudget;
        }

        public void SpendOnRoomEntry(int cost = 1)
        {
            RemainingBudget = Math.Max(0, RemainingBudget - cost);
        }

        public void SpendOnCombatRound(int cost = 1)
        {
            RemainingBudget = Math.Max(0, RemainingBudget - cost);
        }

        /// <summary>Gives time back (Dawn Tincture). Never goes above the night's max.</summary>
        public void Restore(int amount)
        {
            RemainingBudget = Math.Min(MaxBudget, RemainingBudget + amount);
        }
    }
}