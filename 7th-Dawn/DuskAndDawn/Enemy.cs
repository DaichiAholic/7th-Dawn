using System;

namespace DuskAndDawn
{
    public enum EnemyKind
    {
        Wretch,   // common everywhere: light hits, sometimes winds up a heavy blow
        Penitent, // Church Ruins only, in groups: holy spells and a binding chant that stuns
        Knight    // the final night's boss: hits hard and calls Wretches to its side
    }

    /// <summary>What an enemy will do on its next turn. Shown to the player before they act,
    /// so reading it - and answering with Guard, a kill, or smoke - is the heart of combat.</summary>
    public enum IntentType
    {
        Attack,     // a normal hit - Guard halves it
        Charge,     // winding up: harmless now, a Heavy next turn
        Heavy,      // a huge blow - Guard blocks it completely
        Spell,      // holy fire - Guard doesn't help
        Chant,      // a binding chant: harmless now, a Stun next turn
        Stun,       // light damage, and you lose your next turn - unless you Guard
        CallForAid  // the Knight summons a Wretch
    }

    public class Enemy
    {
        public const float HeavyMultiplier = 2.5f;
        public const float SpellMultiplier = 1.3f;
        public const int KnightCallInterval = 5; // turns between calls for aid
        public const int MaxSummonedAllies = 2;

        public string Name { get; }
        public EnemyKind Kind { get; }
        public int MaxHealth { get; }
        public int Health { get; private set; }
        public int AttackPower { get; }

        // Corruption tier 1-3, set by the district. Holy weapons deal bonus damage per point.
        public int Corruption { get; }

        // Summoned by the Knight mid-fight. They drop nothing, so the Knight can't be farmed.
        public bool IsSummoned { get; }

        public IntentType Intent { get; private set; } = IntentType.Attack;

        private int _turnsSinceCall;

        public bool IsDefeated => Health <= 0;
        public bool IsBoss => Kind == EnemyKind.Knight;

        public Enemy(string name, EnemyKind kind, int maxHealth, int attackPower, int corruption = 1, bool isSummoned = false)
        {
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

        /// <summary>Damage this intent will deal before Guard, exposure and smoke.</summary>
        public int IntentDamage => Intent switch
        {
            IntentType.Attack => AttackPower,
            IntentType.Heavy => (int)MathF.Round(AttackPower * HeavyMultiplier),
            IntentType.Spell => (int)MathF.Round(AttackPower * SpellMultiplier),
            IntentType.Stun => Math.Max(1, AttackPower / 2),
            _ => 0
        };

        /// <summary>Picks the next action. A wind-up (Charge, Chant) always leads into its
        /// payoff, so the player gets one full turn of warning before a big hit or a stun.</summary>
        /// <param name="day">Later days make Wretches wind up more often.</param>
        /// <param name="summonedAllies">Living summoned Wretches, so the Knight doesn't flood the room.</param>
        public void PlanNextAction(Random random, int day, int summonedAllies)
        {
            if (Intent == IntentType.Charge) { Intent = IntentType.Heavy; return; }
            if (Intent == IntentType.Chant) { Intent = IntentType.Stun; return; }

            int roll = random.Next(100);
            switch (Kind)
            {
                case EnemyKind.Wretch:
                    Intent = roll < DayInfo.WretchChargeChance(day) ? IntentType.Charge : IntentType.Attack;
                    break;

                case EnemyKind.Penitent:
                    Intent = roll < 40 ? IntentType.Attack
                        : roll < 70 ? IntentType.Spell
                        : IntentType.Chant;
                    break;

                case EnemyKind.Knight:
                    _turnsSinceCall++;
                    if (_turnsSinceCall >= KnightCallInterval && summonedAllies < MaxSummonedAllies)
                    {
                        _turnsSinceCall = 0;
                        Intent = IntentType.CallForAid;
                    }
                    else
                    {
                        Intent = roll < 30 ? IntentType.Charge : IntentType.Attack;
                    }
                    break;
            }
        }

        /// <summary>Short label for the intent chip, e.g. "HEAVY 15".</summary>
        public string IntentLabel => Intent switch
        {
            IntentType.Attack => $"Attack {IntentDamage}",
            IntentType.Charge => "Winding up...",
            IntentType.Heavy => $"HEAVY {IntentDamage}",
            IntentType.Spell => $"Spell {IntentDamage}",
            IntentType.Chant => "Chanting...",
            IntentType.Stun => $"STUN {IntentDamage}",
            IntentType.CallForAid => "Calling for aid",
            _ => ""
        };

        /// <summary>One line on how to answer the intent.</summary>
        public string IntentHint => Intent switch
        {
            IntentType.Attack => "Guard halves it",
            IntentType.Charge => "Heavy blow next turn",
            IntentType.Heavy => "Guard blocks it all",
            IntentType.Spell => "Guard won't help",
            IntentType.Chant => "Stun next turn",
            IntentType.Stun => "Guard, or lose a turn",
            IntentType.CallForAid => "A Wretch will join",
            _ => ""
        };

        /// <summary>True when ignoring this intent is dangerous - drawn in alarm colours.</summary>
        public bool IntentIsThreat => Intent is IntentType.Heavy or IntentType.Stun or IntentType.Charge or IntentType.Chant;
    }
}
