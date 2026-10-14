using System;
using System.Collections.Generic;

namespace DuskAndDawn
{
    public sealed record DistrictEnemies(int Health, int HealthPerTier, int Attack, int AttackPerTier, int KnightChance, int PenitentChance);

    public sealed record EnemyScale(float Health, float Attack, int MinHealth = 1, int MinAttack = 1);

    public static class EnemyStats
    {
        public static readonly Dictionary<District, DistrictEnemies> Districts = new Dictionary<District, DistrictEnemies>
        {
            [District.VillageOutskirts] = new(Health: 18, HealthPerTier: 3, Attack: 4, AttackPerTier: 1, KnightChance: 0, PenitentChance: 0),
            [District.ChurchRuins] = new(Health: 34, HealthPerTier: 4, Attack: 8, AttackPerTier: 1, KnightChance: 0, PenitentChance: 25),
            [District.CastleKeep] = new(Health: 50, HealthPerTier: 5, Attack: 12, AttackPerTier: 1, KnightChance: 15, PenitentChance: 20),
            [District.Castle] = new(Health: 54, HealthPerTier: 5, Attack: 12, AttackPerTier: 1, KnightChance: 30, PenitentChance: 25)
        };

        public const float HealthGainPerDay = 0.08f;
        public const int DaysPerAttackGain = 2;

        public const int DepthPerTier = 2;
        public const int MaxTier = 6;

        public static readonly EnemyScale Wretch = new(Health: 1f, Attack: 0.8f, MinAttack: 2);
        public static readonly EnemyScale Penitent = new(Health: 0.45f, Attack: 0.5f, MinHealth: 8, MinAttack: 2);
        public static readonly EnemyScale Knight = new(Health: 1.5f, Attack: 1f, MinAttack: 3);
        public static readonly EnemyScale Choir = new(Health: 0.22f, Attack: 0.35f, MinHealth: 8, MinAttack: 2);
        public const int ChoirTier = 1;

        public const int PenitentGroup = 2;
        public const int PenitentBigGroup = 3;
        public const int PenitentBigGroupFromTier = 3;
        public const int PenitentBigGroupFromDay = 5;

        public const int WretchChargeChance = 20;
        public const int WretchChargeChancePerDay = 3;
        public const int KnightChargeChance = 35;
        public const int PenitentAttackChance = 40;
        public const int PenitentSpellChance = 30;

        public const int HeraldHealth = 195;
        public const int HeraldAttack = 10;
        public const float HeraldAscendedAttack = 1.25f;
        public const int HeraldCallEveryTurns = 5;
        public const int HeraldMaxChoir = 2;
        public const int HeraldGatherChance = 18;
        public const int HeraldAscendedGatherChance = 28;
        public const int HeraldBrandChance = 22;
        public const int HeraldAscendedBrandChance = 24;

        public const int BrandBurn = 5;
        public const int AscendedBrandBurn = 6;
        public const int BrandTurns = 3;

        public const float HeavyDamage = 2.5f;
        public const float SpellDamage = 1.3f;
        public const float FlareDamage = 2f;
        public const float StunDamage = 0.5f;
        public const float BrandDamage = 0.5f;

        public static int TierAt(int depth) => Math.Min(MaxTier, depth / DepthPerTier);

        public static (int health, int attack) For(EnemyScale scale, District district, int tier, int day)
        {
            var d = Districts[district];
            int health = (int)MathF.Round((d.Health + d.HealthPerTier * tier) * (1f + HealthGainPerDay * (day - 1)));
            int attack = d.Attack + d.AttackPerTier * tier + (day - 1) / DaysPerAttackGain;
            return (Math.Max(scale.MinHealth, (int)MathF.Round(health * scale.Health)),
                Math.Max(scale.MinAttack, (int)MathF.Round(attack * scale.Attack)));
        }

        public static int EffectivePenitentChance(District district)
        {
            var d = Districts[district];
            return d.PenitentChance * (100 - d.KnightChance) / 100;
        }
    }
}
