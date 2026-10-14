using System;

namespace DuskAndDawn
{
    public enum EnemyKind
    {
        Wretch,
        Penitent,
        Knight,
        Herald
    }

    public enum IntentType
    {
        Attack,
        Charge,
        Heavy,
        Spell,
        Chant,
        Stun,
        CallForAid,
        Gather,
        Flare,
        Brand,
        Staggered
    }

    public class Enemy
    {
        public const float HeavyMultiplier = 2.5f;
        public const float SpellMultiplier = 1.3f;
        public const float FlareMultiplier = 2.0f;
        public const float AscendedAttackMultiplier = 1.25f;
        public const int BrandTurns = 3;
        public const int HeraldCallInterval = 5;
        public const int MaxSummonedAllies = 2;

        public string Name { get; }
        public EnemyKind Kind { get; }
        public int MaxHealth { get; }
        public int Health { get; private set; }
        public int AttackPower { get; }

        public int Corruption { get; }

        public bool IsSummoned { get; }

        public IntentType Intent { get; private set; } = IntentType.Attack;

        private int _turnsSinceCall;
        private bool _flaredLast;

        public int BleedDamage { get; private set; }
        public int BleedTurns { get; private set; }

        public bool Ascended { get; private set; }

        public bool IsDefeated => Health <= 0;
        public bool IsBoss { get; }

        public bool IsElite => Kind == EnemyKind.Knight && !IsBoss;

        public Enemy(string name, EnemyKind kind, int maxHealth, int attackPower, int corruption = 1, bool isSummoned = false, bool isBoss = false)
        {
            IsBoss = isBoss;
            Name = name;
            Kind = kind;
            MaxHealth = maxHealth;
            Health = maxHealth;
            AttackPower = attackPower;
            Corruption = corruption;
            IsSummoned = isSummoned;
        }

        public void TakeDamage(int amount)
        {
            Health = Math.Max(0, Health - amount);
        }

        public void ApplyBleed(int damagePerTurn, int turns)
        {
            BleedDamage = Math.Max(BleedDamage, damagePerTurn);
            BleedTurns = Math.Max(BleedTurns, turns);
        }

        public int TickBleed()
        {
            if (BleedTurns <= 0 || IsDefeated) return 0;
            BleedTurns--;
            int damage = Math.Min(BleedDamage, Health);
            TakeDamage(damage);
            if (BleedTurns == 0) BleedDamage = 0;
            return damage;
        }

        public void Stagger() => Intent = IntentType.Staggered;

        public bool TryAscend()
        {
            if (Kind != EnemyKind.Herald || Ascended || IsDefeated || Health > MaxHealth / 2) return false;
            Ascended = true;
            Intent = IntentType.Gather;
            return true;
        }

        public int EffectiveAttack => Ascended ? (int)MathF.Round(AttackPower * AscendedAttackMultiplier) : AttackPower;

        public int BrandBurn => Ascended ? 6 : 5;

        public int IntentDamage => Intent switch
        {
            IntentType.Attack => EffectiveAttack,
            IntentType.Heavy => (int)MathF.Round(EffectiveAttack * HeavyMultiplier),
            IntentType.Spell => (int)MathF.Round(EffectiveAttack * SpellMultiplier),
            IntentType.Stun => Math.Max(1, EffectiveAttack / 2),
            IntentType.Flare => (int)MathF.Round(EffectiveAttack * FlareMultiplier),
            IntentType.Brand => Math.Max(1, EffectiveAttack / 2),
            _ => 0
        };

        public void PlanNextAction(Random random, int day, int summonedAllies)
        {
            if (Intent == IntentType.Charge) { Intent = IntentType.Heavy; return; }
            if (Intent == IntentType.Chant) { Intent = IntentType.Stun; return; }
            if (Intent == IntentType.Gather) { Intent = IntentType.Flare; _flaredLast = true; return; }

            int roll = random.Next(100);
            switch (Kind)
            {
                case EnemyKind.Wretch:
                    Intent = roll < DayInfo.WretchChargeChance(day) ? IntentType.Charge : IntentType.Attack;
                    break;

                case EnemyKind.Penitent:
                    Intent = roll < 40 ? IntentType.Attack
                        : roll < 70 || IsSummoned ? IntentType.Spell
                        : IntentType.Chant;
                    break;

                case EnemyKind.Knight:
                    Intent = roll < 35 ? IntentType.Charge : IntentType.Attack;
                    break;

                case EnemyKind.Herald:
                    {
                        _turnsSinceCall++;
                        bool mayGather = !_flaredLast;
                        _flaredLast = false;
                        if (_turnsSinceCall >= HeraldCallInterval && summonedAllies < MaxSummonedAllies)
                        {
                            _turnsSinceCall = 0;
                            Intent = IntentType.CallForAid;
                            break;
                        }
                        int gather = mayGather ? (Ascended ? 28 : 18) : 0;
                        int brand = Ascended ? 24 : 22;
                        Intent = roll < gather ? IntentType.Gather
                            : roll < gather + brand ? IntentType.Brand
                            : IntentType.Attack;
                        break;
                    }
            }
        }

        public string IntentLabel => Intent switch
        {
            IntentType.Attack => $"Attack {IntentDamage}",
            IntentType.Charge => "Winding up...",
            IntentType.Heavy => $"HEAVY {IntentDamage}",
            IntentType.Spell => $"Spell {IntentDamage}",
            IntentType.Chant => "Chanting...",
            IntentType.Stun => $"STUN {IntentDamage}",
            IntentType.CallForAid => Kind == EnemyKind.Herald ? "Calling the choir" : "Calling for aid",
            IntentType.Gather => "Gathering light...",
            IntentType.Flare => $"SOLAR FLARE {IntentDamage}",
            IntentType.Brand => $"BRAND {IntentDamage} +burn",
            IntentType.Staggered => "Staggered",
            _ => ""
        };

        public string IntentHint => Intent switch
        {
            IntentType.Attack => "Guard halves it",
            IntentType.Charge => "Heavy blow next turn",
            IntentType.Heavy => "Guard blocks it all",
            IntentType.Spell => "Guard won't help",
            IntentType.Chant => "Stun next turn",
            IntentType.Stun => "Guard, or lose a turn",
            IntentType.CallForAid => Kind == EnemyKind.Herald ? "A Penitent will join" : "A Wretch will join",
            IntentType.Gather => "Solar Flare next turn",
            IntentType.Flare => "Guard won't help - smoke",
            IntentType.Brand => $"Burns {BrandBurn}/turn - Guard blocks",
            IntentType.Staggered => "Loses this turn",
            _ => ""
        };

        public bool IntentIsThreat => Intent is IntentType.Heavy or IntentType.Stun or IntentType.Charge or IntentType.Chant
            or IntentType.Gather or IntentType.Flare or IntentType.Brand;
    }
}
