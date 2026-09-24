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

        public RoomGenerator(District district, int? seed = null)
        {
            _district = district;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public RoomType PickNext(int depth)
        {
            var weights = GetWeights(depth);
            return WeightedPick(weights);
        }

        private Dictionary<RoomType, int> GetWeights(int depth)
        {
            int encounterWeight = 40 + depth * 5;
            var (supplies, encounter, special) = DistrictInfo.WeightSkew(_district);

            return new Dictionary<RoomType, int>
            {
                { RoomType.Supplies, Math.Max(1, 45 + supplies) },
                { RoomType.Encounter, Math.Max(1, encounterWeight + encounter) },
                { RoomType.Special, Math.Max(1, 10 + special) }
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