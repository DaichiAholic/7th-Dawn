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
    // Night screen: fights - starting them, the action menu, pacing the exchange, and how they end.
    public partial class NightScavengingScreen
    {
        // Combat sub-state
        private CombatEncounter _activeCombat;
        private CombatMenu _combatMenu = CombatMenu.TopLevel;
        private readonly List<Button> _combatButtons = new List<Button>();

        // Items menu groups identical items into one button ("Bandage x2"), so the button
        // label no longer matches an item name - this holds the real name for each button.
        private readonly List<string> _itemButtonNames = new List<string>();

        // ---- Combat sprite effects ----
        // All three sheets are 7 frames of 64x64. Timings are per whole animation.
        // Both the player's slash and the enemy's hit on the player play big, in the middle
        // of the screen, at the same scale so they read as equal-weight beats.
        private const float AttackAnimDuration = 0.35f;   // quick slash
        private const float SkillAnimDuration = 0.5f;     // heavier, reads as a bigger move
        private const float AttackedAnimDuration = 0.42f;
        private const int EffectScale = 4;                // native size: 64px per frame on screen

        // Settings > Combat Speed: scales every combat animation and the action lock with it.
        private static float AnimScale => GameSettings.Current.CombatAnimScale;

        // Combat actions are locked until the current exchange has finished playing (our
        // animation, then the enemy's hit), plus a short breather - so attacks can't be spammed.
        private const float ActionGap = 0.15f;
        private const float MinActionLock = 0.3f;
        private float _actionLock;
        private bool IsActionLocked => _actionLock > 0f;
        private static readonly Vector2 ScreenCenter = new Vector2(640, 360);

        private readonly SpriteEffect _castEffect = new SpriteEffect();     // Attack / Skill - when we hit the enemy
        private readonly SpriteEffect _playerHitFlash = new SpriteEffect(); // Attacked - when the enemy hits us
        private readonly DiceRollPopup _diceRollPopup = new DiceRollPopup();
        private readonly ImpactShake _enemyShake = new ImpactShake();  // punches up our own hit landing
        private readonly ImpactShake _playerShake = new ImpactShake(); // punches up the enemy's counter-hit landing

        // The enemy's counter-attack is resolved instantly under the hood (same method call
        // as our own action), but showing both hit-flashes at once reads as simultaneous
        // rather than two separate blows. This delays the player-hit-flash, its log line, and
        // its shake so the enemy's counter visibly lands a beat after ours instead of on top
        // of it.
        private const float EnemyCounterDelay = 0.45f;
        private float _pendingPlayerHitDelay = -1f;
        private string _pendingEnemyReplyText = "";

        private const float PlayerHpBarScale = 0.25f;
        private const float EnemyHpBarScale = 0.3125f;
        private static readonly Vector2 PlayerHpBarPosition = new Vector2(40, 122);
        private static readonly Vector2 EnemyHpBarPosition = new Vector2(480, 225);

        // ---------- Encounter (combat) ----------

        private void StartEncounter(MapNode room)
        {
            var enemy = room.Enemy;
            if (enemy == null)
            {
                // The maze runs deeper than the old 6-layer map; halve the step count so enemy
                // strength lands in the same range it was tuned for.
                int depth = room.Depth;
                int tier = Math.Min(6, depth / 2);
                enemy = new Enemy(
                    $"{DistrictInfo.RandomEnemyName(_district, _random)} (Depth {depth})",
                    maxHealth: DistrictInfo.EnemyHealth(_district, tier),
                    attackPower: DistrictInfo.EnemyAttack(_district, tier),
                    corruption: DistrictInfo.Corruption(_district));
            }
            else
            {
                _textLog.Push($"{enemy.Name} is still here, waiting for you. ({enemy.Health}/{enemy.MaxHealth})");
            }
            room.Enemy = null; // put back by Retreat() if you run again
            _activeCombat = new CombatEncounter(enemy, _playerState, _dawnTimer, _random);
            _enemyHealthBar = new LerpBar(enemy.Health, enemy.MaxHealth);
            _combatMenu = CombatMenu.TopLevel;
            _combatTurn = 0;
            LayoutCombatButtons();
            _state = ExplorationState.Encounter;
        }

        private void LayoutCombatButtons()
        {
            _combatButtons.Clear();
            const float x = 40, width = 240, height = 60, gap = 14;
            float y = 230;

            switch (_combatMenu)
            {
                case CombatMenu.TopLevel:
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Attack")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Skills")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Items")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Flee"));
                    break;

                case CombatMenu.Skills:
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Power Strike (risky)")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Guard")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Back"));
                    break;

                case CombatMenu.Items:
                    {
                        // Grouped by name, and a bit more compact than the other menus, since
                        // the Infirmary and Kitchen can stock up to six different items.
                        const float itemHeight = 48, itemGap = 8;
                        _itemButtonNames.Clear();
                        foreach (var group in _playerState.Items.GroupBy(it => it.Name))
                        {
                            int count = group.Count();
                            string itemLabel = count > 1 ? $"{group.Key} x{count}" : group.Key;
                            _combatButtons.Add(new Button(new RectangleF(x, y, width, itemHeight), itemLabel));
                            _itemButtonNames.Add(group.Key);
                            y += itemHeight + itemGap;
                        }
                        _combatButtons.Add(new Button(new RectangleF(x, y, width, itemHeight), "Back"));
                        break;
                    }
            }
        }

        private void HandleCombatClick(int x, int y)
        {
            foreach (var button in _combatButtons)
            {
                if (!button.Contains(x, y)) continue;

                button.TriggerPress();
                var label = button.Label;

                if (_combatMenu == CombatMenu.TopLevel)
                {
                    switch (label)
                    {
                        case "Attack":
                            _textLog.Push(_activeCombat.Attack());
                            _combatTurn++;
                            TriggerCombatEffects(Game1.AttackTexture);
                            break;
                        case "Skills":
                            _combatMenu = CombatMenu.Skills;
                            LayoutCombatButtons();
                            return;
                        case "Items":
                            _combatMenu = CombatMenu.Items;
                            LayoutCombatButtons();
                            return;
                        case "Flee":
                            _textLog.Push(_activeCombat.Flee());
                            EndCombat(fled: true);
                            return;
                    }
                }
                else if (_combatMenu == CombatMenu.Skills)
                {
                    if (label == "Back") { _combatMenu = CombatMenu.TopLevel; LayoutCombatButtons(); return; }
                    var skill = label.StartsWith("Power") ? SkillType.PowerStrike : SkillType.Guard;
                    _textLog.Push(_activeCombat.UseSkill(skill));
                    _combatTurn++;
                    TriggerCombatEffects(Game1.SkillTexture);
                }
                else if (_combatMenu == CombatMenu.Items)
                {
                    if (label == "Back") { _combatMenu = CombatMenu.TopLevel; LayoutCombatButtons(); return; }
                    int itemIndex = _combatButtons.IndexOf(button);
                    string itemName = itemIndex >= 0 && itemIndex < _itemButtonNames.Count ? _itemButtonNames[itemIndex] : label;
                    var item = _playerState.Items.Find(it => it.Name == itemName);
                    if (item != null)
                    {
                        _textLog.Push(_activeCombat.UseItem(item));
                        _combatTurn++;
                        TriggerCombatEffects(Game1.AttackTexture);
                    }
                    _combatMenu = CombatMenu.TopLevel;
                }

                if (_activeCombat.IsOver)
                {
                    EndCombat(fled: false);
                }
                else
                {
                    LayoutCombatButtons();
                }

                return;
            }
        }

        /// <summary>Plays the delayed "enemy hits us" beat: Attacked animation, shake, and the
        /// enemy's log line.</summary>
        private void FirePendingPlayerHit()
        {
            _pendingPlayerHitDelay = -1f;
            _playerHitFlash.Play(Game1.AttackedTexture, ScreenCenter, AttackedAnimDuration * AnimScale, EffectScale);
            _playerShake.Play();
            if (!string.IsNullOrEmpty(_pendingEnemyReplyText))
            {
                _textLog.Push(_pendingEnemyReplyText);
                _pendingEnemyReplyText = "";
            }
        }

        /// <summary>Fires the dice-roll popup and the enemy/player hit-flashes based on what
        /// CombatEncounter's last action actually did - castTexture (Attack.png or
        /// Skill.png) only plays if that action was a damage roll (it's ignored for
        /// Guard, items, etc. since LastRollWasAttack stays false for those).</summary>
        private void TriggerCombatEffects(Texture2D castTexture)
        {
            // If the last enemy hit is still waiting on its delay when a new action comes in
            // (clicking faster than EnemyCounterDelay), play it now. Before, the new action
            // simply reset the timer, so with quick clicks the Attacked animation never played.
            if (_pendingPlayerHitDelay >= 0f)
            {
                FirePendingPlayerHit();
            }

            var portraitCenter = new Vector2(630, 440);

            if (_activeCombat.LastRollWasAttack)
            {
                float castDuration = (castTexture == Game1.SkillTexture ? SkillAnimDuration : AttackAnimDuration) * AnimScale;
                _castEffect.Play(castTexture, ScreenCenter, castDuration, EffectScale);
                _enemyShake.Play();
                _diceRollPopup.Play(Game1.DiceTexture, _activeCombat.LastPlayerRoll, portraitCenter + new Vector2(-70, -90));
            }

            if (_activeCombat.PlayerWasHit)
            {
                // Scheduled rather than played immediately - see EnemyCounterDelay above.
                _pendingPlayerHitDelay = EnemyCounterDelay * AnimScale;
                _pendingEnemyReplyText = _activeCombat.LastEnemyReplyText;
            }
            else if (!string.IsNullOrEmpty(_activeCombat.LastEnemyReplyText))
            {
                // No counter-attack to wait for (the enemy's already defeated) - nothing to
                // stagger against, so the resolution line shows right away.
                _textLog.Push(_activeCombat.LastEnemyReplyText);
            }

            // Lock the buttons until this whole exchange has played out.
            float ourPart = _activeCombat.LastRollWasAttack
                ? (castTexture == Game1.SkillTexture ? SkillAnimDuration : AttackAnimDuration)
                : 0f;
            float enemyPart = _activeCombat.PlayerWasHit ? EnemyCounterDelay + AttackedAnimDuration : 0f;
            _actionLock = Math.Max(MinActionLock, Math.Max(ourPart, enemyPart) + ActionGap) * AnimScale;
        }

        private void EndCombat(bool fled)
        {
            bool won = !fled && _activeCombat.PlayerWon;
            bool knockedOut = !fled && _playerState.Health <= 0;
            var enemy = _activeCombat.Enemy;

            // The fight is over - cut any animation still playing and drop the queued enemy
            // hit (its log line still shows, so the last blow isn't lost from the log).
            _castEffect.Stop();
            _playerHitFlash.Stop();
            if (_pendingPlayerHitDelay >= 0f && !string.IsNullOrEmpty(_pendingEnemyReplyText))
            {
                _textLog.Push(_pendingEnemyReplyText);
            }
            _pendingPlayerHitDelay = -1f;
            _pendingEnemyReplyText = "";
            _actionLock = 0f;

            _activeCombat = null;
            _combatMenu = CombatMenu.TopLevel;
            _state = ExplorationState.Map;

            if (knockedOut)
            {
                Collapse();
                return;
            }
            if (won)
            {
                _roomsCleared++;

                // Fights are the main source of Scraps: the better your weapon, the more of
                // them you can win before your health (or the night) runs out.
                var (lootFood, lootPlanks, lootScraps) = DistrictInfo.CombatLoot(_district, enemy.MaxHealth, _random);
                _playerState.AddResources(lootFood, lootPlanks, lootScraps);
                _textLog.Push($"You strip the remains: {MaterialYield.Describe(lootFood, lootPlanks, lootScraps)}.");
            }
            if (fled)
            {
                Retreat(enemy);
            }
            if (IsNightOver)
            {
                GoToDawnReturn();
            }
        }

        /// <summary>Fleeing backs you out into the room you came from. The creature stays
        /// put - still wounded - and its room counts as unexplored again: the corridor through
        /// it is blocked, and getting past means going back in (and paying the time again).</summary>
        private void Retreat(Enemy enemy)
        {
            var room = _current;
            room.Type = RoomType.Encounter;
            room.Enemy = enemy;
            room.Visited = false;
            room.StirAmount = 1f;
            _roomsVisited = Math.Max(0, _roomsVisited - 1);

            var back = _cameFrom != null && _cameFrom.Visited ? _cameFrom : room.Links.FirstOrDefault(l => l.Visited);
            if (back != null)
            {
                _walkPath = new List<MapNode> { back };
                _walkIndex = 0;
            }

            _textLog.Push($"You back out the way you came. {enemy.Name} is still in there ({enemy.Health}/{enemy.MaxHealth}).");
        }

        /// <summary>Knocked out: half of tonight's haul is dropped in the dark, the house
        /// loses a little Hope, and the night ends.</summary>
        private void Collapse()
        {
            var dropped = new ResourceDelta(
                -Math.Max(0, _playerState.Food - _startFood) / 2,
                -Math.Max(0, _playerState.Planks - _startPlanks) / 2,
                -Math.Max(0, _playerState.Scraps - _startScraps) / 2,
                -KnockoutHopeLoss);
            var lost = _playerState.Apply(dropped);
            _playerState.Health = 1;

            _collapseText = $"You collapse. Someone drags you home before dawn. Lost: {lost.Describe()}.";
            _collapseTimer = CollapseDuration;
            _state = ExplorationState.Map;
        }

    }
}
