using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class MorningEvent
    {
        public string Title { get; }
        public string Description { get; }

        public int FoodCost, PlanksCost, ScrapsCost, HopeCost;
        public int FoodReward, PlanksReward, ScrapsReward, HopeReward;

        public MorningEvent(string title, string description,
            int foodCost = 0, int planksCost = 0, int scrapsCost = 0, int hopeCost = 0,
            int foodReward = 0, int planksReward = 0, int scrapsReward = 0, int hopeReward = 0)
        {
            Title = title;
            Description = description;
            FoodCost = foodCost; PlanksCost = planksCost; ScrapsCost = scrapsCost; HopeCost = hopeCost;
            FoodReward = foodReward; PlanksReward = planksReward; ScrapsReward = scrapsReward; HopeReward = hopeReward;
        }

        public string CostLabel()
        {
            var parts = new List<string>();
            if (FoodCost > 0) parts.Add($"{FoodCost} Food");
            if (PlanksCost > 0) parts.Add($"{PlanksCost} Planks");
            if (ScrapsCost > 0) parts.Add($"{ScrapsCost} Scraps");
            if (HopeCost > 0) parts.Add($"{HopeCost} Hope");
            return parts.Count > 0 ? string.Join(", ", parts) : "Nothing";
        }

        public string RewardLabel()
        {
            var parts = new List<string>();
            if (FoodReward > 0) parts.Add($"{FoodReward} Food");
            if (PlanksReward > 0) parts.Add($"{PlanksReward} Planks");
            if (ScrapsReward > 0) parts.Add($"{ScrapsReward} Scraps");
            if (HopeReward > 0) parts.Add($"{HopeReward} Hope");
            return parts.Count > 0 ? string.Join(", ", parts) : "Nothing";
        }
    }
}
