using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public static class MorningEventPool
    {
        public static readonly List<MorningEvent> All = new List<MorningEvent>
        {
            new MorningEvent("A trader passes by", "Offers food in exchange for scraps you've stockpiled.",
                scrapsCost: 5, foodReward: 8),

            new MorningEvent("Scavengers need food", "They'll trade planks for some of your food stores.",
                foodCost: 6, planksReward: 10),

            new MorningEvent("A stranger asks for shelter", "Taking them in costs food but lifts everyone's spirits.",
                foodCost: 4, hopeReward: 10),

            new MorningEvent("A risky deal", "Good materials on offer, but the deal leaves everyone uneasy.",
                scrapsCost: 8, planksReward: 15, hopeCost: 5),

            new MorningEvent("Quiet morning", "Nothing happens today.")
        };

        public static MorningEvent GetRandom(Random random)
        {
            return All[random.Next(All.Count)];
        }
    }
}
