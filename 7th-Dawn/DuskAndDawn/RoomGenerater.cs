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
        private readonly int _day;

        public RoomGenerator(District district, Random random, int day = 1)
        {
            _district = district;
            _random = random;
            _day = day;
        }

        /// <param name="depth">Steps from the entrance along the maze.</param>
        /// <param name="isDeadEnd">Dead ends lean toward loot, so poking into a corner
        /// is a gamble worth taking rather than just wasted time.</param>
        public RoomType PickNext(int depth, bool isDeadEnd)
        {
            var weights = GetWeights(depth, isDeadEnd);
            return WeightedPick(weights);
        }

        /// <summary>Chance (0-1) of each room type at a given depth - shown on the Prepare
        /// screen's district tooltips.</summary>
        public static (float supplies, float encounter, float special, float empty) Odds(District district, int depth, bool isDeadEnd = false, int day = 1)
        {
            var weights = GetWeights(district, depth, isDeadEnd, day);
            float total = weights.Values.Sum();
            return (weights[RoomType.Supplies] / total, weights[RoomType.Encounter] / total,
                weights[RoomType.Special] / total, weights[RoomType.Empty] / total);
        }

        private Dictionary<RoomType, int> GetWeights(int depth, bool isDeadEnd) => GetWeights(_district, depth, isDeadEnd, _day);

        private static Dictionary<RoomType, int> GetWeights(District district, int depth, bool isDeadEnd, int day)
        {
            // The maze is deeper than the old 6-layer map, so depth is squashed into the
            // same rough 1-6 band the weights were tuned for.
            int tier = Math.Min(6, 1 + depth / 3);
            // Each day more of the ruins stir.
            int encounterWeight = 40 + tier * 5 + DayInfo.EncounterWeightBonus(day);
            var (supplies, encounter, special) = DistrictInfo.WeightSkew(district);

            int deadEndLoot = isDeadEnd ? 20 : 0;

            return new Dictionary<RoomType, int>
            {
                { RoomType.Supplies, Math.Max(1, 45 + supplies + deadEndLoot) },
                { RoomType.Encounter, Math.Max(1, encounterWeight + encounter - (isDeadEnd ? 10 : 0)) },
                // Strange Rooms hold the night's events - common enough to meet a few each night.
                { RoomType.Special, Math.Max(1, 18 + special + deadEndLoot / 2) },
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
