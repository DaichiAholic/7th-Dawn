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

        // Corruption tier 1-3, set by the district. Holy weapons deal bonus damage per point.
        public int Corruption { get; }

        public bool IsDefeated => Health <= 0;

        public Enemy(string name, int maxHealth, int attackPower, int corruption = 1)
        {
            Name = name;
            MaxHealth = maxHealth;
            Health = maxHealth;
            AttackPower = attackPower;
            Corruption = corruption;
        }

        public void TakeDamage(int amount)
        {
            Health = Math.Max(0, Health - amount);
        }
    }
}