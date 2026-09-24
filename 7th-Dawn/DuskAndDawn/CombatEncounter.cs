using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class CombatEncounter
    {
        private readonly PlayerState _playerState;
        private readonly DawnTimer _dawnTimer;
        private readonly Random _random;

        private bool _guardActive;
        private bool _smokeActive;

        // Barracks rerolls left in this fight. When a roll comes in below the weapon's
        // average, one charge is spent automatically and the better of the two rolls is kept.
        private int _rerollsLeft;

        public Enemy Enemy { get; }

        public bool IsOver => Enemy.IsDefeated || _playerState.Health <= 0;
        public bool PlayerWon => Enemy.IsDefeated && _playerState.Health > 0;

        public int RerollsLeft => _rerollsLeft;

        // ---- Last-action results, for the UI to react to (dice popup, hit-flash effects) ----
        // These mirror numbers this class already computes internally; nothing here changes
        // combat math, it just exposes the result of the most recent action so the screen can
        // decide what to animate. Reset at the top of every action, then set as applicable.
        public int LastPlayerRoll { get; private set; }
        public bool LastRollWasAttack { get; private set; }
        public bool EnemyWasHit { get; private set; }
        public bool PlayerWasHit { get; private set; }
        public int LastIncomingDamage { get; private set; }

        // The enemy's reply ("X hits back for 6." / "X is defeated!") used to be appended
        // onto the returned log string. It's kept separate now so the screen can show it as
        // its own log line instead of one run-on sentence.
        public string LastEnemyReplyText { get; private set; } = "";

        public CombatEncounter(Enemy enemy, PlayerState playerState, DawnTimer dawnTimer, Random random)
        {
            Enemy = enemy;
            _playerState = playerState;
            _dawnTimer = dawnTimer;
            _random = random;
            _rerollsLeft = playerState.BarracksRerolls;
        }

        public string Attack()
        {
            ResetLastActionEffects();

            var weapon = _playerState.EquippedWeapon;
            int damage = RollWeapon(out bool rerolled);
            Enemy.TakeDamage(damage);

            LastPlayerRoll = damage;
            LastRollWasAttack = true;
            EnemyWasHit = true;

            string log = $"You roll {weapon.DiceLabel} with your {weapon.Name} - {damage} damage.";
            if (rerolled) log += " (Barracks reroll)";
            _dawnTimer.SpendOnCombatRound();
            ResolveEnemyReply();
            return log;
        }

        public string UseSkill(SkillType skill)
        {
            ResetLastActionEffects();

            string log;
            switch (skill)
            {
                case SkillType.PowerStrike:
                    var weapon = _playerState.EquippedWeapon;
                    int damage = RollWeapon(out bool rerolledA) + RollWeapon(out bool rerolledB);
                    Enemy.TakeDamage(damage);
                    LastPlayerRoll = damage;
                    LastRollWasAttack = true;
                    EnemyWasHit = true;
                    log = $"Power Strike: two rolls of {weapon.DiceLabel} - {damage} damage!";
                    if (rerolledA || rerolledB) log += " (Barracks reroll)";
                    _dawnTimer.SpendOnCombatRound(2);
                    break;

                case SkillType.Guard:
                    _guardActive = true;
                    log = "You brace behind your guard, ready to absorb the next hit.";
                    _dawnTimer.SpendOnCombatRound();
                    break;

                default:
                    log = "Nothing happens.";
                    break;
            }

            ResolveEnemyReply();
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
                        _playerState.Health = Math.Min(_playerState.MaxHealth, _playerState.Health + item.Amount);
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
                case ItemEffect.RestoreDawn:
                    // Restored before the round's tick is spent, so the net gain is Amount - 1.
                    _dawnTimer.Restore(item.Amount);
                    log = $"You drink the {item.Name}. The night feels a little longer.";
                    break;
                default:
                    log = "Nothing happens.";
                    break;
            }

            _playerState.Items.Remove(item);
            _dawnTimer.SpendOnCombatRound();
            ResolveEnemyReply();
            return log;
        }

        public string Flee()
        {
            ResetLastActionEffects();
            _dawnTimer.SpendOnCombatRound();
            return "You break off and flee.";
        }

        /// <summary>One weapon roll with every Barracks modifier and holy-weapon bonus
        /// applied. Spends a reroll charge automatically on a below-average roll.</summary>
        private int RollWeapon(out bool rerolled)
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

            return roll + weapon.CorruptionBonus * Enemy.Corruption;
        }

        private void ResetLastActionEffects()
        {
            LastPlayerRoll = 0;
            LastRollWasAttack = false;
            EnemyWasHit = false;
            PlayerWasHit = false;
            LastIncomingDamage = 0;
            LastEnemyReplyText = "";
        }

        private void ResolveEnemyReply()
        {
            if (Enemy.IsDefeated)
            {
                LastEnemyReplyText = $"{Enemy.Name} is defeated!";
                return;
            }

            if (_smokeActive)
            {
                _smokeActive = false;
                LastEnemyReplyText = $"{Enemy.Name} swings blindly through the smoke and misses.";
                return;
            }

            int incoming = Enemy.AttackPower;
            if (_guardActive)
            {
                incoming /= 2;
                _guardActive = false;
            }

            _playerState.Health = Math.Max(0, _playerState.Health - incoming);
            PlayerWasHit = true;
            LastIncomingDamage = incoming;
            LastEnemyReplyText = $"{Enemy.Name} hits back for {incoming}.";
        }
    }
}