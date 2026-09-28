using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    // Night screen: fights - starting them, the action menu, targeting, pacing the exchange,
    // and how they end.
    public partial class NightScavengingScreen
    {
        // Combat sub-state
        private CombatEncounter _activeCombat;
        private CombatMenu _combatMenu = CombatMenu.TopLevel;
        private readonly List<Button> _combatButtons = new List<Button>();

        // Items menu groups identical items into one button ("Bandage x2"), so the button
        // label no longer matches an item name - this holds the real name for each button.
        private readonly List<string> _itemButtonNames = new List<string>();

        // One animated health bar per enemy in the fight (summoned ones get theirs on arrival).
        private readonly Dictionary<Enemy, LerpBar> _enemyBars = new Dictionary<Enemy, LerpBar>();

        // Set when the fight is the Knight guarding the Hoard: winning opens the Hoard.
        private bool _fightingForHoard;

        private const string ShakeItOffLabel = "Shake it off (stunned)";

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
        private readonly ImpactShake _playerShake = new ImpactShake(); // punches up the enemies' counter-hit landing
        private Enemy _shakenEnemy;                                     // which panel _enemyShake moves

        // The enemies' turn is resolved instantly under the hood (same method call as our own
        // action), but showing both hit-flashes at once reads as simultaneous rather than two
        // separate beats. This delays the player-hit-flash, its log line, and its shake so the
        // counter visibly lands a beat after ours.
        private const float EnemyCounterDelay = 0.45f;
        private float _pendingPlayerHitDelay = -1f;
        private string _pendingEnemyReplyText = "";

        private const float PlayerHpBarScale = 0.25f;
        private static readonly Vector2 PlayerHpBarPosition = new Vector2(40, 122);

        // ---------- Encounter (combat) ----------

        private void StartEncounter(MapNode room)
        {
            var enemies = room.Enemies;
            if (enemies == null || enemies.All(e => e.IsDefeated))
            {
                // The maze runs deeper than the old 6-layer map; halve the step count so enemy
                // strength lands in the same range it was tuned for.
                int tier = Math.Min(6, room.Depth / 2);
                enemies = EnemyRoster.RollEncounter(_district, tier, _playerState.Day, _random);
                _textLog.Push(EncounterIntro(enemies));
            }
            else
            {
                enemies = enemies.Where(e => !e.IsDefeated).ToList();
                bool wounded = enemies.Any(e => e.Health < e.MaxHealth);
                _textLog.Push(enemies.Any(e => e.IsBoss) ? "The Knight lowers its visor. It has been waiting."
                    : wounded ? $"{DescribeGroup(enemies)} still here, waiting for you."
                    : EncounterIntro(enemies)); // identified ahead of time by the Archive's bestiary
            }

            room.Enemies = null; // put back by Retreat() if you run again
            _fightingForHoard = room.Type == RoomType.Hoard;

            // Summons are shallow-tier Wretches whatever the room's depth, so the Hoard's
            // depth doesn't turn every call for aid into a second boss.
            const int summonTier = 1;
            _activeCombat = new CombatEncounter(enemies, _playerState, _dawnTimer, _random, _playerState.Day,
                () => EnemyRoster.Wretch(_district, summonTier, _playerState.Day, _random, summoned: true));

            _enemyBars.Clear();
            foreach (var enemy in enemies) _enemyBars[enemy] = new LerpBar(enemy.Health, enemy.MaxHealth);

            _combatMenu = CombatMenu.TopLevel;
            _combatTurn = 0;
            LayoutCombatButtons();
            _state = ExplorationState.Encounter;
        }

        private static string EncounterIntro(List<Enemy> enemies)
        {
            var first = enemies[0];
            return first.Kind switch
            {
                EnemyKind.Penitent => $"{enemies.Count} Penitents turn from their prayers, chanting as one.",
                EnemyKind.Knight when first.IsBoss => "The Hollow Knight rises from the Hoard's throne.",
                EnemyKind.Knight => $"An {first.Name} steps out of the dark, blade already raised.",
                _ => $"A {first.Name} lurches out of the dark."
            };
        }

        private static string DescribeGroup(List<Enemy> enemies) =>
            enemies.Count == 1 ? $"{enemies[0].Name} is" : $"{enemies.Count} of them are";

        private void LayoutCombatButtons()
        {
            _combatButtons.Clear();
            const float x = 40, width = 240, height = 60, gap = 14;
            float y = 230;

            if (_activeCombat != null && _activeCombat.PlayerStunned)
            {
                _combatButtons.Add(new Button(new RectangleF(x, y, width, height), ShakeItOffLabel));
                return;
            }

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
                        // Tall enough for the name plus a line on what it does.
                        const float itemHeight = 56, itemGap = 6;
                        _itemButtonNames.Clear();
                        foreach (var group in _playerState.Belt.GroupBy(it => it.Name))
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

        /// <summary>Where each enemy's panel sits - shared by drawing and click-to-target.
        /// Up to three side by side, centred in the space right of the action buttons.</summary>
        private static RectangleF EnemyPanelBounds(int index, int count)
        {
            // Starts below the status card, which reaches y 210 on the left.
            const float areaX = 300f, areaWidth = 940f, gap = 18f, top = 222f, height = 326f;
            float width = count <= 1 ? 320f : Math.Min(290f, (areaWidth - gap * (count - 1)) / count);
            float total = width * count + gap * (count - 1);
            float x = areaX + (areaWidth - total) / 2f + index * (width + gap);
            return new RectangleF(x, top, width, height);
        }

        private void HandleCombatClick(int x, int y)
        {
            // Clicking an enemy makes it the target.
            var enemies = _activeCombat.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (!enemies[i].IsDefeated && InputChecker.Contains(EnemyPanelBounds(i, enemies.Count), x, y))
                {
                    _activeCombat.SetTarget(enemies[i]);
                    return;
                }
            }

            foreach (var button in _combatButtons)
            {
                if (!button.Contains(x, y)) continue;

                button.TriggerPress();
                var label = button.Label;

                if (label == ShakeItOffLabel)
                {
                    _textLog.Push(_activeCombat.Recover());
                    _combatTurn++;
                    TriggerCombatEffects(Game1.AttackTexture);
                }
                else if (_combatMenu == CombatMenu.TopLevel)
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
                            _playerState.ChangeHope(-PlayerState.FleeHopeLoss);
                            _textLog.Push($"{_activeCombat.Flee()} Hope -{PlayerState.FleeHopeLoss}.");
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
                    _combatMenu = CombatMenu.TopLevel;
                }
                else if (_combatMenu == CombatMenu.Items)
                {
                    if (label == "Back") { _combatMenu = CombatMenu.TopLevel; LayoutCombatButtons(); return; }
                    int itemIndex = _combatButtons.IndexOf(button);
                    string itemName = itemIndex >= 0 && itemIndex < _itemButtonNames.Count ? _itemButtonNames[itemIndex] : label;
                    var item = _playerState.Belt.Find(it => it.Name == itemName);
                    if (item != null)
                    {
                        _textLog.Push(_activeCombat.UseItem(item));
                        _combatTurn++;
                        TriggerCombatEffects(Game1.AttackTexture);
                    }
                    _combatMenu = CombatMenu.TopLevel;
                }

                // A Knight's call for aid can add an enemy mid-fight.
                foreach (var enemy in _activeCombat.Enemies)
                {
                    if (!_enemyBars.ContainsKey(enemy)) _enemyBars[enemy] = new LerpBar(enemy.Health, enemy.MaxHealth);
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

        /// <summary>Plays the delayed "enemies hit us" beat: Attacked animation, shake, and the
        /// enemies' log line.</summary>
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

        /// <summary>Fires the dice-roll popup and the hit flashes based on what the last action
        /// actually did - castTexture (Attack.png or Skill.png) only plays for a damage roll.</summary>
        private void TriggerCombatEffects(Texture2D castTexture)
        {
            // If the last enemy hit is still waiting on its delay when a new action comes in,
            // play it now so quick clicks never skip it.
            if (_pendingPlayerHitDelay >= 0f)
            {
                FirePendingPlayerHit();
            }

            if (_activeCombat.LastRollWasAttack)
            {
                float castDuration = (castTexture == Game1.SkillTexture ? SkillAnimDuration : AttackAnimDuration) * AnimScale;
                _castEffect.Play(castTexture, ScreenCenter, castDuration, EffectScale);
                _shakenEnemy = _activeCombat.LastTarget;
                _enemyShake.Play();

                int index = Math.Max(0, _activeCombat.Enemies.IndexOf(_activeCombat.LastTarget));
                var panel = EnemyPanelBounds(index, _activeCombat.Enemies.Count);
                _diceRollPopup.Play(Game1.DiceTexture, _activeCombat.LastPlayerRoll, new Vector2(panel.X + panel.Width / 2f, panel.Y + 120));
            }

            if (_activeCombat.PlayerWasHit)
            {
                // Scheduled rather than played immediately - see EnemyCounterDelay above.
                _pendingPlayerHitDelay = EnemyCounterDelay * AnimScale;
                _pendingEnemyReplyText = _activeCombat.LastEnemyReplyText;
            }
            else if (!string.IsNullOrEmpty(_activeCombat.LastEnemyReplyText))
            {
                // Nothing hit us (all blocked, winding up, or they're all down) - no blow to
                // stagger against, so the enemies' line shows right away.
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
            var combat = _activeCombat;
            bool won = !fled && combat.PlayerWon;
            bool knockedOut = !fled && _playerState.Health <= 0;

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
                if (!_fightingForHoard) _roomsCleared++; // the Hoard counts itself below
                CollectCombatLoot(combat);
                if (_fightingForHoard)
                {
                    ResolveHoard();
                }
            }
            if (fled)
            {
                Retreat(combat.Living.ToList());
            }
            _fightingForHoard = false;

            if (IsNightOver)
            {
                GoToDawnReturn();
            }
        }

        /// <summary>Fights are the main source of Scraps: every fallen enemy (except the
        /// Knight's summons) is stripped, so a weapon that wins without bleeding out pays.</summary>
        private void CollectCombatLoot(CombatEncounter combat)
        {
            int food = 0, planks = 0, scraps = 0;
            foreach (var enemy in combat.Enemies.Where(e => !e.IsSummoned))
            {
                var (f, p, s) = DistrictInfo.CombatLoot(_district, enemy.MaxHealth, _random);
                food += f;
                planks += p;
                scraps += s;
            }
            _playerState.AddResources(food, planks, scraps);
            _textLog.Push($"You strip the remains: {MaterialYield.Describe(food, planks, scraps)}.");

            if (combat.HasBoss)
            {
                _playerState.KnightSlain = true;
                _playerState.ChangeHope(PlayerState.KnightSlainHope);
                _textLog.Push($"The Hollow Knight falls, and the whole ruin seems to exhale. Hope +{PlayerState.KnightSlainHope}.");
            }
        }

        /// <summary>Fleeing backs you out into the room you came from. The enemies stay put -
        /// still wounded - and the room counts as unexplored again: the corridor through it is
        /// blocked, and getting past means going back in (and paying the time again).</summary>
        private void Retreat(List<Enemy> survivors)
        {
            var room = _current;
            if (room.Type != RoomType.Hoard) room.Type = RoomType.Encounter;
            room.Enemies = survivors;
            room.Visited = false;
            room.StirAmount = 1f;
            _roomsVisited = Math.Max(0, _roomsVisited - 1);

            var back = _cameFrom != null && _cameFrom.Visited ? _cameFrom : room.Links.FirstOrDefault(l => l.Visited);
            if (back != null)
            {
                _walkPath = new List<MapNode> { back };
                _walkIndex = 0;
            }

            _textLog.Push($"You back out the way you came. {DescribeGroup(survivors)} still in there.");
        }

        /// <summary>Knocked out: half of tonight's haul is dropped in the dark, the house
        /// loses Hope, and the night ends.</summary>
        private void Collapse()
        {
            var dropped = new ResourceDelta(
                -Math.Max(0, _playerState.Food - _startFood) / 2,
                -Math.Max(0, _playerState.Planks - _startPlanks) / 2,
                -Math.Max(0, _playerState.Scraps - _startScraps) / 2,
                -PlayerState.KnockoutHopeLoss);
            var lost = _playerState.Apply(dropped);
            _playerState.Health = 1;

            _collapseText = $"You collapse. Someone drags you home before dawn. Lost: {lost.Describe()}.";
            _collapseTimer = CollapseDuration;
            _state = ExplorationState.Map;
        }
    }
}
