using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    // Night screen: supply caches - every way of dealing with one trades time, loot, risk and health.
    public partial class NightScavengingScreen
    {
        // Supplies sub-state. Every cache's contents are rolled up front and shown, and each
        // way of dealing with it trades time, loot, risk and health differently.
        private enum SupplyAction { Grab, Thorough, Quiet, Rest, Leave }

        private const int GrabMinutes = 15;
        private const int ThoroughMinutes = 45;
        private const int QuietMinutes = 90;
        private const int RestMinutes = 40;
        private const int ThoroughNoiseChance = 35;   // % chance the noise starts a fight
        private const int ThoroughRemedyChance = 30;  // % chance of a bonus item
        private const int RestHeal = 30;

        private ResourceDelta _cache;
        private readonly List<SupplyAction> _supplyActions = new List<SupplyAction>();
        private readonly List<Button> _suppliesButtons = new List<Button>();

        // ---------- Supplies ----------

        private void StartSupplies()
        {
            // Two finds' worth of this district's materials, rolled up front so every choice
            // below can say exactly what it gets you.
            var (food, planks, scraps) = DistrictInfo.Yield(_district).Roll(_random, times: 2);
            // Storage Lv 5 supply runs: every cache holds a quarter more.
            float bonus = _playerState.SupplyCacheMultiplier;
            _cache = new ResourceDelta((int)MathF.Round(food * bonus), (int)MathF.Round(planks * bonus), (int)MathF.Round(scraps * bonus));

            _supplyActions.Clear();
            _supplyActions.AddRange(new[] { SupplyAction.Grab, SupplyAction.Thorough, SupplyAction.Quiet, SupplyAction.Rest, SupplyAction.Leave });

            _suppliesButtons.Clear();
            const float x = 420, width = 820, height = 76, gap = 8;
            float y = 204;
            foreach (var action in _supplyActions)
            {
                _suppliesButtons.Add(new Button(new RectangleF(x, y, width, height), SupplyTitle(action))
                {
                    Enabled = SupplyBlockedReason(action) == null
                });
                y += height + gap;
            }
            _state = ExplorationState.Supplies;
        }

        private static int SupplyMinutes(SupplyAction action) => action switch
        {
            SupplyAction.Grab => GrabMinutes,
            SupplyAction.Thorough => ThoroughMinutes,
            SupplyAction.Quiet => QuietMinutes,
            SupplyAction.Rest => RestMinutes,
            _ => 0
        };

        private static string SupplyTitle(SupplyAction action) => action switch
        {
            SupplyAction.Grab => "Grab what's in reach",
            SupplyAction.Thorough => "Search thoroughly",
            SupplyAction.Quiet => "Search quietly",
            SupplyAction.Rest => "Rest among the crates",
            _ => "Leave it"
        };

        // Half of each pile, rounded up.
        // Half of each pile, rounded up - or all of it with Storage Lv 3 packframes.
        private ResourceDelta HalfCache => _playerState.StoragePackframes
            ? _cache
            : new ResourceDelta((_cache.Food + 1) / 2, (_cache.Planks + 1) / 2, (_cache.Scraps + 1) / 2);

        private int RestHealAmount => Math.Min(RestHeal, _playerState.MaxHealth - _playerState.Health);

        private string SupplyDescription(SupplyAction action) => action switch
        {
            SupplyAction.Grab => _playerState.StoragePackframes
                ? $"Everything, strapped to your packframe: {HalfCache.Describe()}. Quick and safe."
                : $"About half: {HalfCache.Describe()}. Quick and safe.",
            SupplyAction.Thorough => $"Everything, {ThoroughRemedyChance}% chance of a remedy - but {ThoroughNoiseChance}% chance the noise brings a fight.",
            SupplyAction.Quiet => "Everything, without a sound. Safe, but it eats the night.",
            SupplyAction.Rest => $"Take nothing. Bar the door and bind your wounds: +{RestHealAmount} health.",
            _ => "Take nothing and lose no time."
        };

        /// <summary>Why an option can't be picked right now, or null if it can. Grabbing an
        /// armful is always allowed, so the last room before dawn is never wasted.</summary>
        private string SupplyBlockedReason(SupplyAction action)
        {
            if (action == SupplyAction.Rest && RestHealAmount <= 0) return "You're unhurt";
            if (action != SupplyAction.Grab && !_dawnTimer.CanAfford(SupplyMinutes(action))) return "Dawn would break first";
            return null;
        }

        private void HandleSuppliesClick(int x, int y)
        {
            for (int i = 0; i < _suppliesButtons.Count; i++)
            {
                // Button.Contains is already false for blocked options.
                if (!_suppliesButtons[i].Contains(x, y)) continue;

                _suppliesButtons[i].TriggerPress();
                ResolveSupply(_supplyActions[i]);
                return;
            }
        }

        private void ResolveSupply(SupplyAction action)
        {
            _dawnTimer.Spend(SupplyMinutes(action));
            bool drewAFight = false;
            string text;

            switch (action)
            {
                case SupplyAction.Grab:
                    text = $"You grab what's in easy reach. {_playerState.Apply(HalfCache).Describe()}.";
                    break;

                case SupplyAction.Thorough:
                    {
                        text = $"You turn the place over. {_playerState.Apply(_cache).Describe()}.";
                        if (_random.Next(100) < ThoroughRemedyChance)
                        {
                            var remedies = new Func<Item>[] { Item.Bandage, Item.Tonic, Item.SmokeFlask };
                            var remedy = remedies[_random.Next(remedies.Length)]();
                            _playerState.Items.Add(remedy);
                            text += $" Tucked at the bottom: a {remedy.Name}.";
                        }
                        if (_random.Next(100) < ThoroughNoiseChance)
                        {
                            drewAFight = true;
                            text += " Something heard all that...";
                        }
                        break;
                    }

                case SupplyAction.Quiet:
                    text = $"Slowly, silently, you empty the cache. {_playerState.Apply(_cache).Describe()}.";
                    break;

                case SupplyAction.Rest:
                    {
                        int healed = RestHealAmount;
                        _playerState.Health += healed;
                        text = $"You bar the door and bind your wounds. +{healed} health.";
                        break;
                    }

                default:
                    text = "You leave it for another night.";
                    break;
            }

            _textLog.Push(text);
            WarnOfDawn();

            // A fight drawn by the noise decides whether this room counts as cleared.
            if (drewAFight)
            {
                StartEncounter(_current);
                return;
            }
            _roomsCleared++;

            _state = ExplorationState.Map;
            if (IsNightOver)
            {
                GoToDawnReturn();
            }
        }

    }
}
