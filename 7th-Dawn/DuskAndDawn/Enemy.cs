using System;

namespace DuskAndDawn
{
    public enum EnemyKind
    {
        Wretch,   // common everywhere: light hits, sometimes winds up a heavy blow
        Penitent, // Church Ruins only, in groups: holy spells and a binding chant that stuns
        Knight,   // hits hard and winds up heavy blows - an elite of the Keep and the Castle
        Herald    // the Sun Herald, the final boss: solar flares, a searing brand, a choir of
                  // Penitents, and a second phase at half health
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
        CallForAid, // a boss summons help (the Herald's choir: a Penitent)
        Gather,     // the Herald gathers the light: harmless now, a Solar Flare next turn
        Flare,      // Solar Flare - huge, and Guard doesn't help. Smoke, or be ready to heal
        Brand,      // Searing Brand: a light hit that sets you burning for a few turns - Guard blocks it
        Staggered   // knocked off balance by a stagger weapon: does nothing this turn
    }

    public class Enemy
    {
        public const float HeavyMultiplier = 2.5f;
        public const float SpellMultiplier = 1.3f;
        public const float FlareMultiplier = 2.0f;
        public const float AscendedAttackMultiplier = 1.25f;
        public const int BrandTurns = 3;
        public const int HeraldCallInterval = 5;  // turns between calls to the choir
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
        private bool _flaredLast;

        // Bleeding from a Bleed weapon: this much damage at the start of each of its next
        // BleedTurns turns. A fresh cut refreshes the timer and keeps the worse wound.
        public int BleedDamage { get; private set; }
        public int BleedTurns { get; private set; }

        /// <summary>The Herald's second phase, from half health: hits 25% harder, gathers
        /// the light more often, and his brand burns hotter.</summary>
        public bool Ascended { get; private set; }

        public bool IsDefeated => Health <= 0;
        // The final night's Sun Herald. Knights are elites: hard, but they fight alone.
        public bool IsBoss { get; }

        /// <summary>A Knight that isn't the boss - tagged ELITE in fights.</summary>
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

        /// <summary>One turn of bleeding. Returns the damage taken (0 if not bleeding).</summary>
        public int TickBleed()
        {
            if (BleedTurns <= 0 || IsDefeated) return 0;
            BleedTurns--;
            int damage = Math.Min(BleedDamage, Health);
            TakeDamage(damage);
            if (BleedTurns == 0) BleedDamage = 0;
            return damage;
        }

        /// <summary>Knocked off balance: whatever it was about to do - a wind-up included -
        /// is lost this turn.</summary>
        public void Stagger() => Intent = IntentType.Staggered;

        /// <summary>The Herald crossing half health: he ascends, and at once starts gathering
        /// the light for a flare. True only the moment it happens.</summary>
        public bool TryAscend()
        {
            if (Kind != EnemyKind.Herald || Ascended || IsDefeated || Health > MaxHealth / 2) return false;
            Ascended = true;
            Intent = IntentType.Gather;
            return true;
        }

        /// <summary>Attack power with the Herald's second phase applied.</summary>
        public int EffectiveAttack => Ascended ? (int)MathF.Round(AttackPower * AscendedAttackMultiplier) : AttackPower;

        /// <summary>Burn per turn from a Searing Brand.</summary>
        public int BrandBurn => Ascended ? 6 : 5;

        /// <summary>Damage this intent will deal before Guard, exposure and smoke.</summary>
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

        /// <summary>Picks the next action. A wind-up (Charge, Chant) always leads into its
        /// payoff, so the player gets one full turn of warning before a big hit or a stun.</summary>
        /// <param name="day">Later days make Wretches wind up more often.</param>
        /// <param name="summonedAllies">Living summoned allies, so a boss doesn't flood the room.</param>
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
                    // The Herald's choir sing fire, but never the binding chant - a boss that
                    // can also stun-lock you would be a coin toss, not a fight.
                    Intent = roll < 40 ? IntentType.Attack
                        : roll < 70 || IsSummoned ? IntentType.Spell
                        : IntentType.Chant;
                    break;

                case EnemyKind.Knight:
                    // An elite fights alone and winds up often.
                    Intent = roll < 35 ? IntentType.Charge : IntentType.Attack;
                    break;

                case EnemyKind.Herald:
                    {
                        _turnsSinceCall++;
                        // Never two flares back to back - there's always a turn to recover.
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

        /// <summary>Short label for the intent chip, e.g. "HEAVY 15".</summary>
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

        /// <summary>One line on how to answer the intent.</summary>
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

        /// <summary>True when ignoring this intent is dangerous - drawn in alarm colours.</summary>
        public bool IntentIsThreat => Intent is IntentType.Heavy or IntentType.Stun or IntentType.Charge or IntentType.Chant
            or IntentType.Gather or IntentType.Flare or IntentType.Brand;
    }
}
