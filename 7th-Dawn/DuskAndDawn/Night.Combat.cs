using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    public partial class NightScavengingScreen
    {
        private CombatEncounter _activeCombat;
        public bool InCombat => _activeCombat != null;
        private CombatMenu _combatMenu = CombatMenu.TopLevel;
        private readonly List<Button> _combatButtons = new List<Button>();

        private readonly List<string> _itemButtonNames = new List<string>();

        private readonly Dictionary<Enemy, LerpBar> _enemyBars = new Dictionary<Enemy, LerpBar>();

        private readonly HashSet<Enemy> _placedAllies = new HashSet<Enemy>();
        private readonly Enemy[] _sideOccupants = new Enemy[2];

        private bool _fightingForHoard;

        private const string ShakeItOffLabel = "Shake it off (stunned)";
        private const string NoEscapeLabel = "No escape";

        private const float AttackAnimDuration = 0.35f;
        private const float SkillAnimDuration = 0.5f;
        private const float AttackedAnimDuration = 0.42f;
        private const int EffectScale = 4;

        private static float AnimScale => GameSettings.Current.CombatAnimScale;

        private const float ActionGap = 0.15f;
        private const float MinActionLock = 0.3f;
        private float _actionLock;
        private bool IsActionLocked => _actionLock > 0f;
        private static readonly Vector2 ScreenCenter = new Vector2(640, 360);

        private readonly SpriteEffect _castEffect = new SpriteEffect();
        private readonly SpriteEffect _playerHitFlash = new SpriteEffect();
        private readonly DiceRollPopup _diceRollPopup = new DiceRollPopup();
        private readonly ImpactShake _enemyShake = new ImpactShake();
        private readonly ImpactShake _playerShake = new ImpactShake();
        private Enemy _shakenEnemy;

        private const float EnemyCounterDelay = 0.45f;
        private float _pendingPlayerHitDelay = -1f;
        private string _pendingEnemyReplyText = "";
        private float _pendingEnemySoundDelay = -1f;
        private readonly List<Sfx> _pendingEnemySounds = new List<Sfx>();

        private const int PlayerHpBarScreenScale = 4;
        private static readonly Vector2 PlayerHpBarPosition = new Vector2(40, 122);

        private void StartEncounter(MapNode room)
        {
            var enemies = room.Enemies;
            if (enemies == null || enemies.All(e => e.IsDefeated))
            {
                enemies = EnemyRoster.RollEncounter(_district, EnemyStats.TierAt(room.Depth), _playerState.Day, _random);
                _textLog.Push(EncounterIntro(enemies));
            }
            else
            {
                enemies = enemies.Where(e => !e.IsDefeated).ToList();
                bool wounded = enemies.Any(e => e.Health < e.MaxHealth);
                _textLog.Push(enemies.Any(e => e.IsBoss) ? EncounterIntro(enemies)
                    : wounded ? $"{DescribeGroup(enemies)} still here, waiting for you."
                    : EncounterIntro(enemies));
            }

            room.Enemies = null;
            _fightingForHoard = room.Type == RoomType.Hoard;

            Func<Enemy> summon = enemies.Any(e => e.Kind == EnemyKind.Herald)
                ? () => EnemyRoster.ChoirAcolyte(_district, _playerState.Day, _random)
                : null;
            _activeCombat = new CombatEncounter(enemies, _playerState, _dawnTimer, _random, _playerState.Day, summon);

            _enemyBars.Clear();
            _placedAllies.Clear();
            Array.Clear(_sideOccupants);
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
                EnemyKind.Herald => "The Sun Herald rises from his throne, and the hall floods with light. The doors seal behind you.",
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
                    bool canFlee = _activeCombat == null || _activeCombat.CanFlee;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), canFlee ? "Flee" : NoEscapeLabel) { Enabled = canFlee });
                    break;

                case CombatMenu.Skills:
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Power Strike (risky)")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Guard")); y += height + gap;
                    _combatButtons.Add(new Button(new RectangleF(x, y, width, height), "Back"));
                    break;

                case CombatMenu.Items:
                    {
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

        private const float PanelAreaX = 300f, PanelAreaWidth = 940f, PanelGap = 18f, PanelTop = 222f, PanelHeight = 326f;
        private const float WidePanelWidth = 320f, PanelWidth = 290f;

        private static RectangleF EnemyRowBounds(int index, int count)
        {
            float width = count <= 1 ? WidePanelWidth : Math.Min(PanelWidth, (PanelAreaWidth - PanelGap * (count - 1)) / count);
            float total = width * count + PanelGap * (count - 1);
            float x = PanelAreaX + (PanelAreaWidth - total) / 2f + index * (width + PanelGap);
            return new RectangleF(x, PanelTop, width, PanelHeight);
        }

        private List<(Enemy enemy, RectangleF bounds)> EnemyPanels()
        {
            var enemies = _activeCombat.Enemies;
            var boss = enemies.FirstOrDefault(e => e.IsBoss);
            if (boss == null)
            {
                return enemies.Select((enemy, i) => (enemy, EnemyRowBounds(i, enemies.Count))).ToList();
            }

            foreach (var ally in enemies)
            {
                if (ally == boss || _placedAllies.Contains(ally)) continue;
                int side = Array.FindIndex(_sideOccupants, occupant => occupant == null || occupant.IsDefeated);
                if (side < 0) continue;
                _placedAllies.Add(ally);
                _sideOccupants[side] = ally;
            }

            float center = PanelAreaX + PanelAreaWidth / 2f;
            var bossBounds = new RectangleF(center - WidePanelWidth / 2f, PanelTop, WidePanelWidth, PanelHeight);
            var panels = new List<(Enemy, RectangleF)> { (boss, bossBounds) };
            if (_sideOccupants[0] != null)
            {
                panels.Add((_sideOccupants[0], new RectangleF(bossBounds.X - PanelGap - PanelWidth, PanelTop, PanelWidth, PanelHeight)));
            }
            if (_sideOccupants[1] != null)
            {
                panels.Add((_sideOccupants[1], new RectangleF(bossBounds.Right + PanelGap, PanelTop, PanelWidth, PanelHeight)));
            }
            return panels;
        }

        private void HandleCombatClick(int x, int y)
        {
            foreach (var (enemy, bounds) in EnemyPanels())
            {
                if (!enemy.IsDefeated && InputChecker.Contains(bounds, x, y))
                {
                    _activeCombat.SetTarget(enemy);
                    return;
                }
            }

            foreach (var button in _combatButtons)
            {
                if (!button.Contains(x, y)) continue;
                PressCombatButton(button);
                return;
            }
        }

        private static readonly Keys[][] ButtonKeys =
        {
            new[] { Keys.D1, Keys.NumPad1 }, new[] { Keys.D2, Keys.NumPad2 }, new[] { Keys.D3, Keys.NumPad3 },
            new[] { Keys.D4, Keys.NumPad4 }, new[] { Keys.D5, Keys.NumPad5 }, new[] { Keys.D6, Keys.NumPad6 },
            new[] { Keys.D7, Keys.NumPad7 }, new[] { Keys.D8, Keys.NumPad8 }, new[] { Keys.D9, Keys.NumPad9 }
        };

        private void HandleCombatKeys(Func<Keys, bool> pressed, bool shift)
        {
            for (int i = 0; i < ButtonKeys.Length && i < _combatButtons.Count; i++)
            {
                if (ButtonKeys[i].Any(pressed))
                {
                    PressCombatButton(_combatButtons[i]);
                    return;
                }
            }

            if (pressed(Keys.Space))
            {
                var quick = _activeCombat.PlayerStunned
                    ? _combatButtons.FirstOrDefault(b => b.Label == ShakeItOffLabel)
                    : _combatMenu == CombatMenu.TopLevel ? _combatButtons.FirstOrDefault(b => b.Label == "Attack") : null;
                if (quick != null) PressCombatButton(quick);
                return;
            }

            if (pressed(Keys.Tab))
            {
                var living = _activeCombat.Enemies.Where(e => !e.IsDefeated).ToList();
                if (living.Count > 1)
                {
                    int index = living.IndexOf(_activeCombat.Target);
                    index = (index + (shift ? -1 : 1) + living.Count) % living.Count;
                    _activeCombat.SetTarget(living[index]);
                }
                return;
            }

            if (pressed(Keys.Back) && _combatMenu != CombatMenu.TopLevel)
            {
                _combatMenu = CombatMenu.TopLevel;
                LayoutCombatButtons();
            }
        }

        private void PressCombatButton(Button button)
        {
            if (!button.Enabled) return;
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
        }

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

        private static Sfx? EnemyActionSound(Enemy enemy, IntentType action) => (enemy.Kind, action) switch
        {
            (EnemyKind.Herald, IntentType.Attack) => Sfx.BossAttack,
            (EnemyKind.Herald, IntentType.Brand or IntentType.Flare or IntentType.CallForAid) => Sfx.BossSkill,
            (EnemyKind.Penitent, IntentType.Attack) => Sfx.PenitentAttack,
            (EnemyKind.Penitent, IntentType.Spell or IntentType.Stun) => Sfx.PenitentSkill,
            (EnemyKind.Knight or EnemyKind.Wretch, IntentType.Attack or IntentType.Heavy) => Sfx.KnightWretchAttack,
            _ => null
        };

        private void PlayPendingEnemySounds()
        {
            _pendingEnemySoundDelay = -1f;
            foreach (var sound in _pendingEnemySounds) Sound.Play(sound);
            _pendingEnemySounds.Clear();
        }

        private void TriggerCombatEffects(Texture2D castTexture)
        {
            if (_pendingPlayerHitDelay >= 0f)
            {
                FirePendingPlayerHit();
            }
            if (_pendingEnemySoundDelay >= 0f)
            {
                PlayPendingEnemySounds();
            }

            foreach (var (enemy, action) in _activeCombat.LastEnemyActions)
            {
                if (EnemyActionSound(enemy, action) is Sfx sound) _pendingEnemySounds.Add(sound);
            }
            if (_pendingEnemySounds.Count > 0) _pendingEnemySoundDelay = EnemyCounterDelay * AnimScale;

            if (_activeCombat.LastRollWasAttack)
            {
                float castDuration = (castTexture == Game1.SkillTexture ? SkillAnimDuration : AttackAnimDuration) * AnimScale;
                _castEffect.Play(castTexture, ScreenCenter, castDuration, EffectScale);
                Sound.Play(_playerState.EquippedWeapon.HitSound);
                _shakenEnemy = _activeCombat.LastTarget;
                _enemyShake.Play();

                var panels = EnemyPanels();
                var panel = panels.FirstOrDefault(p => p.enemy == _activeCombat.LastTarget).bounds;
                if (panel.Width <= 0f) panel = panels[0].bounds;
                _diceRollPopup.Play(Game1.DiceTexture, _activeCombat.LastPlayerRoll, new Vector2(panel.X + panel.Width / 2f, panel.Y + 120));
            }

            if (_activeCombat.PlayerWasHit)
            {
                _pendingPlayerHitDelay = EnemyCounterDelay * AnimScale;
                _pendingEnemyReplyText = _activeCombat.LastEnemyReplyText;
            }
            else if (!string.IsNullOrEmpty(_activeCombat.LastEnemyReplyText))
            {
                _textLog.Push(_activeCombat.LastEnemyReplyText);
            }

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

            _castEffect.Stop();
            _playerHitFlash.Stop();
            if (_pendingPlayerHitDelay >= 0f && !string.IsNullOrEmpty(_pendingEnemyReplyText))
            {
                _textLog.Push(_pendingEnemyReplyText);
            }
            _pendingPlayerHitDelay = -1f;
            _pendingEnemyReplyText = "";
            if (_pendingEnemySoundDelay >= 0f) PlayPendingEnemySounds();
            _actionLock = 0f;

            _activeCombat = null;
            _combatMenu = CombatMenu.TopLevel;
            _state = ExplorationState.Map;
            _enemiesDefeatedTonight += combat.Enemies.Count(e => e.IsDefeated && !e.IsSummoned);

            if (knockedOut)
            {
                _knockedOutTonight = true;
                if (combat.HasBoss) FallToHerald();
                else Collapse();
                return;
            }
            if (won && _throneRoom)
            {
                _roomsCleared++;
                CollectCombatLoot(combat);
                _fightingForHoard = false;
                WinThrone();
                return;
            }
            if (won)
            {
                if (!_fightingForHoard) _roomsCleared++;
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
            if (food + planks + scraps > 0) Sound.Play(Sfx.ItemLoot);

            if (combat.HasBoss)
            {
                _playerState.HeraldSlain = true;
                _playerState.ChangeHope(PlayerState.HeraldSlainHope);
                _textLog.Push($"The Sun Herald falls, and his light goes out like a snuffed candle. Hope +{PlayerState.HeraldSlainHope}.");
            }
        }

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

        private void FallToHerald()
        {
            _playerState.FellToHerald = true;
            _collapseText = "The Sun Herald's light fills everything, and then there is nothing left of you to burn.";
            _collapseTimer = CollapseDuration;
            _collapseEndsRun = true;
            _state = ExplorationState.Map;
        }

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
