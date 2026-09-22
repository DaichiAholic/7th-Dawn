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

        public Enemy Enemy { get; }

        public bool IsOver => Enemy.IsDefeated || _playerState.Health <= 0;
        public bool PlayerWon => Enemy.IsDefeated && _playerState.Health > 0;

        public CombatEncounter(Enemy enemy, PlayerState playerState, DawnTimer dawnTimer, Random random)
        {
            Enemy = enemy;
            _playerState = playerState;
            _dawnTimer = dawnTimer;
            _random = random;
        }

        public string Attack()
        {
            var weapon = _playerState.EquippedWeapon;
            int damage = weapon.RollDamage(_random);
            Enemy.TakeDamage(damage);

            string log = $"You roll {weapon.DiceLabel} with your {weapon.Name} - {damage} damage.";
            _dawnTimer.SpendOnCombatRound();
            return AppendEnemyReply(log);
        }

        public string UseSkill(SkillType skill)
        {
            string log;
            switch (skill)
            {
                case SkillType.PowerStrike:
                    var weapon = _playerState.EquippedWeapon;
                    int damage = weapon.RollDamage(_random) + weapon.RollDamage(_random);
                    Enemy.TakeDamage(damage);
                    log = $"Power Strike: two rolls of {weapon.DiceLabel} - {damage} damage!";
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

            return AppendEnemyReply(log);
        }

        public string UseItem(Item item)
        {
            _playerState.Health = Math.Min(_playerState.MaxHealth, _playerState.Health + item.HealAmount);
            _playerState.Items.Remove(item);

            string log = $"You use {item.Name} and recover {item.HealAmount} health.";
            _dawnTimer.SpendOnCombatRound();
            return AppendEnemyReply(log);
        }

        public string Flee()
        {
            _dawnTimer.SpendOnCombatRound();
            return "You break off and flee.";
        }

        private string AppendEnemyReply(string log)
        {
            if (Enemy.IsDefeated)
            {
                return log + $" {Enemy.Name} is defeated!";
            }

            int incoming = Enemy.AttackPower;
            if (_guardActive)
            {
                incoming /= 2;
                _guardActive = false;
            }

            _playerState.Health = Math.Max(0, _playerState.Health - incoming);
            return log + $" {Enemy.Name} hits back for {incoming}.";
        }
    }
}
