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
        private readonly District _district;

        public RoomGenerator(District district, Random random)
        {
            _district = district;
            _random = random;
        }

        /// <param name="depth">Steps from the entrance along the maze.</param>
        /// <param name="isDeadEnd">Dead ends lean toward loot, so poking into a corner
        /// is a gamble worth taking rather than just wasted time.</param>
        public RoomType PickNext(int depth, bool isDeadEnd)
        {
            var weights = GetWeights(depth, isDeadEnd);
            return WeightedPick(weights);
        }

        private Dictionary<RoomType, int> GetWeights(int depth, bool isDeadEnd)
        {
            // The maze is deeper than the old 6-layer map, so depth is squashed into the
            // same rough 1-6 band the weights were tuned for.
            int tier = Math.Min(6, 1 + depth / 3);
            int encounterWeight = 40 + tier * 5;
            var (supplies, encounter, special) = DistrictInfo.WeightSkew(_district);

            int deadEndLoot = isDeadEnd ? 20 : 0;

            return new Dictionary<RoomType, int>
            {
                { RoomType.Supplies, Math.Max(1, 45 + supplies + deadEndLoot) },
                { RoomType.Encounter, Math.Max(1, encounterWeight + encounter - (isDeadEnd ? 10 : 0)) },
                { RoomType.Special, Math.Max(1, 10 + special + deadEndLoot / 2) },
                { RoomType.Empty, isDeadEnd ? 8 : 24 }
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
