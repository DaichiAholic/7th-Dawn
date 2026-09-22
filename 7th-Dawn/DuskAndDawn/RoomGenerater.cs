using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class RoomGenerator
    {
        private readonly Random _random;

        public RoomGenerator(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public RoomType PickNext(int depth)
        {
            var weights = GetBaselineWeights(depth);
            return WeightedPick(weights);
        }

        private Dictionary<RoomType, int> GetBaselineWeights(int depth)
        {
            int encounterWeight = 40 + depth * 5;

            return new Dictionary<RoomType, int>
            {
                { RoomType.Supplies, 45 },
                { RoomType.Encounter, encounterWeight },
                { RoomType.Special, 10 }
            };
        }

        private RoomType WeightedPick(Dictionary<RoomType, int> weights)
        {
            int total = 0;
            foreach (var w in weights.Values) total += w;

            int roll = _random.Next(total);
            int cumulative = 0;
            foreach (var kvp in weights)
            {
                cumulative += kvp.Value;
                if (roll < cumulative) return kvp.Key;
            }

            return RoomType.Supplies;
        }
    }
}
