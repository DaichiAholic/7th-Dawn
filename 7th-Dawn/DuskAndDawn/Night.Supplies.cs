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
    public partial class NightScavengingScreen
    {
        private enum SupplyAction { Grab, Thorough, Quiet, Rest, Leave }

        private const int GrabMinutes = 15;
        private const int ThoroughMinutes = 45;
        private const int QuietMinutes = 90;
        private const int RestMinutes = 40;
        private const int ThoroughNoiseChance = 35;
        private const int ThoroughRemedyChance = 30;
        private const int RestHeal = 30;

        private ResourceDelta _cache;
        private readonly List<SupplyAction> _supplyActions = new List<SupplyAction>();
        private readonly List<Button> _suppliesButtons = new List<Button>();

        private void StartSupplies()
        {
            var (food, planks, scraps) = DistrictInfo.Yield(_district).Roll(_random, times: 2);
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
                            string whereItWent = _playerState.GainItemAtNight(remedy);
                            text += $" Tucked at the bottom: a {remedy.Name} - {whereItWent}.";
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
            if (action is SupplyAction.Grab or SupplyAction.Thorough or SupplyAction.Quiet) Sound.Play(Sfx.ItemLoot);

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
