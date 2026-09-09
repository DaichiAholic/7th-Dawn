using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class Enemy
    {
        public string Name { get; }
        public int MaxHealth { get; }
        public int Health { get; private set; }
        public int AttackPower { get; }

        public bool IsDefeated => Health <= 0;

        public Enemy(string name, int maxHealth, int attackPower)
        {
            Name = name;
            MaxHealth = maxHealth;
            Health = maxHealth;
            AttackPower = attackPower;
        }

        public void TakeDamage(int amount)
        {
            Health = Math.Max(0, Health - amount);
        }
    }
}
