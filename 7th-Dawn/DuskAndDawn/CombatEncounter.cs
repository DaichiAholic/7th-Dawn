using System;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    /// <summary>
    /// One fight: the player against one or more enemies. Each round the player acts, then
    /// every living enemy carries out the intent it showed, then picks its next one - so the
    /// player always sees what's coming before choosing. Guard halves normal hits and fully
    /// blocks Heavy blows and Stuns; spells ignore it; smoke makes everything miss.
    /// </summary>
    public class CombatEncounter
    {
        private readonly PlayerState _playerState;
        private readonly DawnTimer _dawnTimer;
        private readonly Random _random;
        private readonly int _day;
        private readonly Func<Enemy> _summonAid;

        private bool _guardActive;
        private bool _smokeActive;

        // The round spent shaking off a stun can't be stunned again - otherwise a group of
        // Penitents could chain chants and lock the player out of the fight.
        private bool _stunImmune;

        // Combat doesn't cost dawn time, so Power Strike's price is risk instead: every hit
        // next round lands this much harder while you're open.
        public const float PowerStrikeExposure = 1.5f;
        private bool _exposed;

        // Barracks rerolls left in this fight. When a roll comes in below the weapon's
        // average, one charge is spent automatically and the better of the two rolls is kept.
        private int _rerollsLeft;

        public List<Enemy> Enemies { get; }
        public IEnumerable<Enemy> Living => Enemies.Where(e => !e.IsDefeated);

        /// <summary>Who Attack and Power Strike hit. Click an enemy to change it; it moves on
        /// by itself when the target falls.</summary>
        public Enemy Target { get; private set; }

        /// <summary>Hit by an unguarded Stun: the next action can only be Recover.</summary>
        public bool PlayerStunned { get; private set; }

        public bool IsOver => Enemies.All(e => e.IsDefeated) || _playerState.Health <= 0;
        public bool PlayerWon => Enemies.All(e => e.IsDefeated) && _playerState.Health > 0;
        public bool HasBoss => Enemies.Any(e => e.IsBoss);

        public int RerollsLeft => _rerollsLeft;

        // ---- Last-action results, for the UI to react to (dice popup, hit flashes) ----
        // Reset at the top of every action, then set as applicable.
        public int LastPlayerRoll { get; private set; }
        public bool LastRollWasAttack { get; private set; }
        public Enemy LastTarget { get; private set; }
        public bool PlayerWasHit { get; private set; }
        public int LastIncomingDamage { get; private set; }

        // The enemies' side of the round ("X hits back for 6. Y begins a chant."), kept
        // separate from the player's line so the screen can show it a beat later.
        public string LastEnemyReplyText { get; private set; } = "";

        /// <param name="day">Scales how often Wretches wind up heavy blows.</param>
        /// <param name="summonAid">Makes the Wretch a Knight calls in. null = nobody answers.</param>
        public CombatEncounter(List<Enemy> enemies, PlayerState playerState, DawnTimer dawnTimer, Random random, int day, Func<Enemy> summonAid)
        {
            Enemies = enemies;
            _playerState = playerState;
            _dawnTimer = dawnTimer;
            _random = random;
            _day = day;
            _summonAid = summonAid;
            _rerollsLeft = playerState.BarracksRerolls;
            Target = Living.FirstOrDefault();
        }

        public void SetTarget(Enemy enemy)
        {
            if (enemy != null && !enemy.IsDefeated && Enemies.Contains(enemy)) Target = enemy;
        }

        public string Attack()
        {
            ResetLastActionEffects();

            var weapon = _playerState.EquippedWeapon;
            var target = Target;
            int damage = RollWeapon(target, out bool rerolled);
            string log = $"You roll {weapon.DiceLabel} with your {weapon.DisplayName} - {damage} damage to {target.Name}.";
            if (rerolled) log += " (Barracks reroll)";
            if (_playerState.WorkshopEdge > 0) log += " (whetstone)";
            log += HitTarget(target, damage);

            ResolveEnemyTurn();
            return log;
        }

        public string UseSkill(SkillType skill)
        {
            ResetLastActionEffects();

            string log;
            switch (skill)
            {
                case SkillType.PowerStrike:
                    {
                        var weapon = _playerState.EquippedWeapon;
                        var target = Target;
                        int damage = RollWeapon(target, out bool rerolledA) + RollWeapon(target, out bool rerolledB);
                        log = $"Power Strike: two rolls of {weapon.DiceLabel} - {damage} damage to {target.Name}! You're left wide open.";
                        if (rerolledA || rerolledB) log += " (Barracks reroll)";
                        log += HitTarget(target, damage);
                        _exposed = true;
                        break;
                    }

                case SkillType.Guard:
                    _guardActive = true;
                    log = "You raise your guard - blows will glance off, and heavy ones won't land at all.";
                    break;

                default:
                    log = "Nothing happens.";
                    break;
            }

            ResolveEnemyTurn();
            return log;
        }

        public string UseItem(Item item)
        {
            ResetLastActionEffects();

            string log;
            switch (item.Effect)
            {
                case ItemEffect.Heal:
                    {
                        int before = _playerState.Health;
                        _playerState.Health = Math.Min(_playerState.MaxHealth, _playerState.Health + item.HealAmount(_playerState));
                        log = $"You use {item.Name} and recover {_playerState.Health - before} health.";
                        break;
                    }
                case ItemEffect.FullHeal:
                    {
                        int before = _playerState.Health;
                        _playerState.Health = _playerState.MaxHealth;
                        log = $"You drink the {item.Name} and recover {_playerState.Health - before} health.";
                        break;
                    }
                case ItemEffect.Smoke:
                    _smokeActive = true;
                    log = $"You shatter the {item.Name}. Thick smoke fills the room.";
                    break;
                case ItemEffect.HolyWater:
                    {
                        var struck = Living.ToList();
                        foreach (var enemy in struck) enemy.TakeDamage(item.Amount);
                        int fallen = struck.Count(e => e.IsDefeated);
                        log = $"You hurl the {item.Name}. It hisses across {(struck.Count == 1 ? "your foe" : $"all {struck.Count} of them")} for {item.Amount} each"
                            + (fallen > 0 ? $" - {fallen} fall{(fallen == 1 ? "s" : "")}!" : ".");
                        if (Target == null || Target.IsDefeated) Target = Living.FirstOrDefault(e => e.IsBoss) ?? Living.FirstOrDefault();
                        break;
                    }
                case ItemEffect.RestoreDawn:
                    _dawnTimer.Restore(item.Amount);
                    log = $"You drink the {item.Name}. The clock slips back {DawnTimer.FormatDuration(item.Amount)}.";
                    break;
                default:
                    log = "Nothing happens.";
                    break;
            }

            _playerState.Items.Remove(item);
            ResolveEnemyTurn();
            return log;
        }

        /// <summary>The only action while stunned: the turn is lost, and the enemies act.</summary>
        public string Recover()
        {
            ResetLastActionEffects();
            PlayerStunned = false;
            _stunImmune = true;
            ResolveEnemyTurn();
            return "The binding holds you a moment longer - then you shake it off.";
        }

        /// <summary>Running costs no time, so it costs blood: the hardest hitter gets a
        /// parting swing at half strength (smoke still covers you). Never knocks you out.</summary>
        public string Flee()
        {
            ResetLastActionEffects();
            if (_smokeActive)
            {
                return "You slip away through the smoke.";
            }

            var chaser = Living.OrderByDescending(e => e.AttackPower).FirstOrDefault();
            int parting = chaser == null ? 0 : Math.Min(chaser.AttackPower / 2, _playerState.Health - 1);
            if (parting <= 0)
            {
                return "You break off and flee.";
            }

            _playerState.Health -= parting;
            return $"You break off and flee - {chaser.Name} catches you for {parting} on the way out.";
        }

        /// <summary>Applies damage to the target and moves the target on if it falls.
        /// Returns any extra log text.</summary>
        private string HitTarget(Enemy target, int damage)
        {
            target.TakeDamage(damage);
            LastPlayerRoll = damage;
            LastRollWasAttack = true;
            LastTarget = target;

            if (!target.IsDefeated) return "";

            Target = Living.FirstOrDefault(e => e.IsBoss) ?? Living.FirstOrDefault();
            return Target != null ? $" {target.Name} falls." : "";
        }

        /// <summary>One weapon roll with every Barracks modifier and holy-weapon bonus
        /// applied. Spends a reroll charge automatically on a below-average roll.</summary>
        private int RollWeapon(Enemy target, out bool rerolled)
        {
            var weapon = _playerState.EquippedWeapon;
            int minFace = _playerState.BarracksMinFace;
            int extraDice = _playerState.BarracksExtraDice;

            int roll = weapon.RollDamage(_random, minFace, extraDice);
            rerolled = false;

            if (_rerollsLeft > 0 && roll < weapon.AverageRoll(minFace, extraDice))
            {
                _rerollsLeft--;
                rerolled = true;
                roll = Math.Max(roll, weapon.RollDamage(_random, minFace, extraDice));
            }

            return roll + weapon.CorruptionBonus * target.Corruption + _playerState.WorkshopEdge;
        }

        /// <summary>Barracks Lv 4: a blocked HEAVY or STUN opens the attacker up for one
        /// free weapon roll (no reroll spent).</summary>
        private void Riposte(Enemy enemy, List<string> lines)
        {
            if (!_playerState.BarracksRiposte || enemy.IsDefeated) return;
            var weapon = _playerState.EquippedWeapon;
            int damage = weapon.RollDamage(_random, _playerState.BarracksMinFace, _playerState.BarracksExtraDice)
                + weapon.CorruptionBonus * enemy.Corruption + _playerState.WorkshopEdge;
            enemy.TakeDamage(damage);
            lines.Add(enemy.IsDefeated ? $"You riposte for {damage} - {enemy.Name} falls!" : $"You riposte for {damage}!");
        }

        private void ResetLastActionEffects()
        {
            LastPlayerRoll = 0;
            LastRollWasAttack = false;
            LastTarget = null;
            PlayerWasHit = false;
            LastIncomingDamage = 0;
            LastEnemyReplyText = "";
        }

        /// <summary>Every living enemy carries out its intent, then plans the next one.</summary>
        private void ResolveEnemyTurn()
        {
            if (Enemies.All(e => e.IsDefeated))
            {
                LastEnemyReplyText = Enemies.Count == 1 ? $"{Enemies[0].Name} is defeated!" : "The last of them falls!";
                ClearRoundModifiers();
                return;
            }

            var lines = new List<string>();
            var joined = new List<Enemy>();
            int totalDamage = 0;

            foreach (var enemy in Living.ToList())
            {
                if (_playerState.Health <= 0) break;

                switch (enemy.Intent)
                {
                    case IntentType.Charge:
                        lines.Add($"{enemy.Name} winds up a heavy blow.");
                        break;

                    case IntentType.Chant:
                        lines.Add($"{enemy.Name} begins a binding chant.");
                        break;

                    case IntentType.CallForAid:
                        {
                            var aid = _summonAid?.Invoke();
                            if (aid != null)
                            {
                                Enemies.Add(aid);
                                joined.Add(aid);
                                lines.Add($"{enemy.Name} bellows into the dark - a Wretch answers.");
                            }
                            else
                            {
                                lines.Add($"{enemy.Name} calls out, but nothing comes.");
                            }
                            break;
                        }

                    default:
                        totalDamage += ResolveHit(enemy, lines);
                        break;
                }
            }

            ClearRoundModifiers();

            int summoned = Living.Count(e => e.IsSummoned);
            foreach (var enemy in Living)
            {
                // Newcomers already picked their opening move - re-planning now would skip a
                // wind-up's warning turn.
                if (!joined.Contains(enemy)) enemy.PlanNextAction(_random, _day, summoned);
            }

            if (Target == null || Target.IsDefeated) Target = Living.FirstOrDefault();

            PlayerWasHit = totalDamage > 0;
            LastIncomingDamage = totalDamage;
            LastEnemyReplyText = string.Join(" ", lines);
        }

        /// <summary>One damaging intent against the player. Returns the damage dealt.</summary>
        private int ResolveHit(Enemy enemy, List<string> lines)
        {
            var intent = enemy.Intent;
            if (_smokeActive)
            {
                lines.Add($"{enemy.Name} strikes blindly through the smoke and misses.");
                return 0;
            }

            int damage = enemy.IntentDamage;
            if (_exposed) damage = (int)MathF.Round(damage * PowerStrikeExposure);

            if (_guardActive)
            {
                switch (intent)
                {
                    case IntentType.Heavy:
                        lines.Add($"{enemy.Name}'s heavy blow crashes into your guard - blocked!");
                        Riposte(enemy, lines);
                        return 0;
                    case IntentType.Stun:
                        lines.Add($"{enemy.Name}'s binding breaks against your guard.");
                        Riposte(enemy, lines);
                        return 0;
                    case IntentType.Attack:
                        damage /= 2;
                        break;
                    // Spells ignore Guard.
                }
            }

            damage = Math.Min(damage, _playerState.Health);
            _playerState.Health -= damage;

            switch (intent)
            {
                case IntentType.Heavy:
                    lines.Add($"{enemy.Name}'s heavy blow lands for {damage}!");
                    break;
                case IntentType.Spell:
                    lines.Add($"{enemy.Name}'s holy fire burns you for {damage}.");
                    break;
                case IntentType.Stun when _stunImmune:
                    lines.Add($"{enemy.Name}'s chant hits for {damage}, but can't take hold again.");
                    break;
                case IntentType.Stun:
                    PlayerStunned = _playerState.Health > 0;
                    lines.Add($"{enemy.Name}'s chant binds you for {damage} - you're stunned!");
                    break;
                default:
                    lines.Add(_guardActive
                        ? $"{enemy.Name} hits your guard for {damage}."
                        : $"{enemy.Name} hits you for {damage}.");
                    break;
            }
            return damage;
        }

        private void ClearRoundModifiers()
        {
            _guardActive = false;
            _smokeActive = false;
            _exposed = false;
            _stunImmune = false;
        }
    }
}
