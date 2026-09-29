using System;
using System.Collections.Generic;
using System.Linq;

namespace DuskAndDawn
{
    /// <summary>What resolving a night event option did.</summary>
    public readonly struct NightEventResult
    {
        public string Text { get; }
        public bool StartsFight { get; }

        public NightEventResult(string text, bool startsFight = false)
        {
            Text = text;
            StartsFight = startsFight;
        }
    }

    /// <summary>One choice in a night event. Minutes and Cost are shown and paid up front;
    /// HealthRisk is the worst the option can do to you, so it can be refused when it
    /// could knock you out.</summary>
    public class NightEventOption
    {
        public string Label { get; }
        public string Detail { get; }
        public int Minutes { get; }
        public ResourceDelta Cost { get; }
        public int HealthRisk { get; }

        private readonly Func<PlayerState, Random, int, NightEventResult> _resolve;

        public NightEventOption(string label, string detail, int minutes, Func<PlayerState, Random, int, NightEventResult> resolve,
            ResourceDelta cost = default, int healthRisk = 0)
        {
            Label = label;
            Detail = detail;
            Minutes = minutes;
            Cost = cost;
            HealthRisk = healthRisk;
            _resolve = resolve;
        }

        /// <summary>Why this can't be picked right now, or null if it can.</summary>
        public string BlockedReason(PlayerState state, DawnTimer timer)
        {
            if (Minutes > 0 && !timer.CanAfford(Minutes)) return "Dawn would break first";
            if (!state.CanAfford(Cost.Food, Cost.Planks, Cost.Scraps)) return $"Needs {Cost.Describe()}";
            if (HealthRisk > 0 && state.Health <= HealthRisk) return "Too hurt to risk it";
            return null;
        }

        /// <summary>Pays the cost and plays out the option. `depth` makes deeper rooms richer.</summary>
        public NightEventResult Resolve(PlayerState state, Random random, int depth)
        {
            state.Apply(-Cost);
            return _resolve(state, random, depth);
        }
    }

    public class NightEvent
    {
        public string Title { get; }
        public string Description { get; }
        public IReadOnlyList<NightEventOption> Options { get; }

        public NightEvent(string title, string description, params NightEventOption[] options)
        {
            Title = title;
            Description = description;
            // Walking away is always possible, and always free.
            Options = options.Append(new NightEventOption("Leave it", "Take nothing and move on.", 0,
                (s, r, d) => new NightEventResult("You leave it be."))).ToList();
        }
    }

    /// <summary>
    /// The Strange Rooms of the night: small stories with a choice. What they offer follows
    /// the district - the Outskirts deal in food and supplies, the Church in timber and
    /// relics, the Keep in metal (and traps), the Castle in gifts with a price - plus a few
    /// that can turn up anywhere.
    /// </summary>
    public static class NightEventPool
    {
        // Deeper rooms pay a little more: +1 to every non-empty pile per 4 steps in.
        private static int Roll(Random r, int min, int max, int depth) => max <= 0 ? 0 : r.Next(min, max + 1) + depth / 4;

        private static NightEventResult Gain(PlayerState s, string text, int food = 0, int planks = 0, int scraps = 0, int hope = 0, int health = 0)
        {
            var got = s.Apply(new ResourceDelta(food, planks, scraps, hope));
            if (health != 0) s.Health = Math.Clamp(s.Health + health, 1, s.MaxHealth);
            string summary = got.Describe();
            if (health > 0) summary += $", +{health} health";
            if (health < 0) summary += $", {health} health";
            return new NightEventResult($"{text} ({summary}.)");
        }

        private static NightEventOption Opt(string label, string detail, int minutes, Func<PlayerState, Random, int, NightEventResult> resolve,
            ResourceDelta cost = default, int healthRisk = 0) =>
            new NightEventOption(label, detail, minutes, resolve, cost, healthRisk);

        // ---------------- Village Outskirts: food and supplies ----------------
        private static readonly Func<NightEvent>[] Village =
        {
            () => new NightEvent("An abandoned orchard", "Gnarled trees behind a fallen wall, a few still heavy with fruit.",
                Opt("Strip the trees", "Lots of Food, slowly.", 45, (s, r, d) => Gain(s, "You fill a sack with bruised apples and pears.", food: Roll(r, 6, 9, d))),
                Opt("Fill your pockets", "A little Food, quickly.", 15, (s, r, d) => Gain(s, "You grab what hangs low.", food: Roll(r, 2, 4, d)))),

            () => new NightEvent("A barricaded pantry", "Someone sealed this larder from the inside and never came out.",
                Opt("Force the door", "60%: a full larder. Otherwise the ceiling comes down (-12 health).", 15, (s, r, d) =>
                    r.Next(100) < 60
                        ? Gain(s, "The door gives. Shelves of jars, still sealed.", food: Roll(r, 8, 11, d))
                        : Gain(s, "The door gives - and so does the ceiling.", food: Roll(r, 2, 3, d), health: -12), healthRisk: 12),
                Opt("Work the hinges loose", "Plenty of Food, safely, but it takes a while.", 50, (s, r, d) => Gain(s, "Pin by pin, the door swings open.", food: Roll(r, 6, 8, d)))),

            () => new NightEvent("A starving survivor", "A gaunt figure hugs the wall, eyes fixed on your pack.",
                Opt("Share your food", "Costs 4 Food. They'll remember it.", 5, (s, r, d) => Gain(s, "They eat, then press a bundle of nails into your hand.", scraps: 3, hope: 6), cost: new ResourceDelta(food: 4)),
                Opt("Trade stories", "A little Hope, a little time.", 20, (s, r, d) => Gain(s, "For a while, the dark feels smaller.", hope: 3)),
                Opt("Walk past", "It weighs on you.", 0, (s, r, d) => Gain(s, "You don't look back.", hope: -2))),

            () => new NightEvent("A root cellar", "A trapdoor under a collapsed kitchen. It smells of earth and onions.",
                Opt("Dig it out", "Food and some timber - a long job.", 60, (s, r, d) => Gain(s, "Sacks of roots, and the old shelving.", food: Roll(r, 7, 10, d), planks: Roll(r, 1, 3, d))),
                Opt("Reach in blind", "70%: a quick handful. Something might bite (-8 health).", 0, (s, r, d) =>
                    r.Next(100) < 70
                        ? Gain(s, "Your hand closes on potatoes.", food: Roll(r, 3, 5, d))
                        : Gain(s, "Something down there bites.", health: -8), healthRisk: 8)),

            () => new NightEvent("An overturned cart", "A merchant's cart lies on its side across the lane.",
                Opt("Right it and search", "Food and Planks.", 30, (s, r, d) => Gain(s, "Most of it spilled, but not all.", food: Roll(r, 3, 5, d), planks: Roll(r, 2, 4, d))),
                Opt("Break it for timber", "Planks.", 20, (s, r, d) => Gain(s, "Good oak, once the wheels are off.", planks: Roll(r, 4, 6, d)))),

            () => new NightEvent("Bees in the mill", "Wild bees have claimed the old mill. The combs drip.",
                Opt("Take the honey", "60%: Food and a little Hope. Otherwise they swarm (-10 health).", 20, (s, r, d) =>
                    r.Next(100) < 60
                        ? Gain(s, "Sticky, sweet, and worth it.", food: Roll(r, 5, 7, d), hope: 2)
                        : Gain(s, "They swarm you all the way out.", food: 2, health: -10), healthRisk: 10)),
        };

        // ---------------- Church Ruins: timber and relics ----------------
        private static readonly Func<NightEvent>[] Church =
        {
            () => new NightEvent("A collapsed nave", "Pews lie splintered beneath a fallen roof beam.",
                Opt("Salvage the pews", "Lots of Planks, slowly.", 45, (s, r, d) => Gain(s, "Old, dry, straight-grained wood.", planks: Roll(r, 6, 9, d))),
                Opt("Take the loose boards", "A few Planks, quickly.", 15, (s, r, d) => Gain(s, "An armful of boards.", planks: Roll(r, 2, 4, d)))),

            () => new NightEvent("A silver reliquary", "A reliquary glints on the altar. Something beneath the altar is breathing.",
                Opt("Pry out the silver", "55%: rich Scraps. Otherwise Penitents wake - a fight.", 10, (s, r, d) =>
                    r.Next(100) < 55
                        ? Gain(s, "The silver comes free without a sound.", scraps: Roll(r, 8, 11, d))
                        : new NightEventResult("The breathing stops. Chanting starts.", startsFight: true)),
                Opt("Leave an offering", "Costs 2 Food. A little peace.", 5, (s, r, d) => Gain(s, "You leave bread on the altar and feel lighter for it.", hope: 5), cost: new ResourceDelta(food: 2))),

            () => new NightEvent("The organ loft", "Brass pipes still stand above the ruined choir.",
                Opt("Cut down the pipes", "Scraps and Planks - a long, loud job.", 60, (s, r, d) => Gain(s, "Brass by the armful, and the loft's railing.", planks: Roll(r, 2, 3, d), scraps: Roll(r, 5, 7, d))),
                Opt("Strip the keyboard", "A few Planks.", 20, (s, r, d) => Gain(s, "Ivory and hardwood.", planks: Roll(r, 3, 4, d)))),

            () => new NightEvent("A confessional", "A carved booth, its curtain still drawn.",
                Opt("Confess", "Hope and a moment's rest.", 20, (s, r, d) => Gain(s, "Nobody answers. You feel lighter anyway.", hope: 5, health: 10)),
                Opt("Break it apart", "Planks, but it feels wrong.", 25, (s, r, d) => Gain(s, "Good wood. You try not to think about it.", planks: Roll(r, 5, 7, d), hope: -2))),

            () => new NightEvent("Scaffolding in the bell tower", "Someone began repairs, long ago. The timber is still up there.",
                Opt("Climb and strip it", "70%: lots of Planks. Otherwise you fall (-14 health).", 40, (s, r, d) =>
                    r.Next(100) < 70
                        ? Gain(s, "You lower it down plank by plank.", planks: Roll(r, 7, 10, d), scraps: 2)
                        : Gain(s, "A rung snaps. The floor is very hard.", planks: 3, health: -14), healthRisk: 14)),
        };

        // ---------------- Castle Keep: metal and traps ----------------
        private static readonly Func<NightEvent>[] Keep =
        {
            () => new NightEvent("An armoury rack", "Rusted blades and mail hang from a toppled rack.",
                Opt("Strip it all", "Lots of Scraps, slowly.", 45, (s, r, d) => Gain(s, "Rust flakes off good steel.", scraps: Roll(r, 8, 11, d))),
                Opt("Grab what's loose", "A few Scraps, quickly.", 15, (s, r, d) => Gain(s, "A few buckles and blades.", scraps: Roll(r, 3, 5, d)))),

            () => new NightEvent("A sealed vault", "A strongroom door, its lock half-melted by something hot.",
                Opt("Force it", "55%: a fortune in Scraps. Otherwise a trap (-16 health).", 20, (s, r, d) =>
                    r.Next(100) < 55
                        ? Gain(s, "Coin, plate and chain, piled to the ceiling.", scraps: Roll(r, 12, 16, d))
                        : Gain(s, "The lock was trapped.", scraps: 3, health: -16), healthRisk: 16),
                Opt("Pick it slowly", "Plenty of Scraps, safely - takes over an hour.", 70, (s, r, d) => Gain(s, "Click by click, it opens.", scraps: Roll(r, 9, 12, d)))),

            () => new NightEvent("A siege engine", "A broken catapult rots in the courtyard.",
                Opt("Dismantle it", "Planks and Scraps - a long job.", 60, (s, r, d) => Gain(s, "Beams, bolts and chain.", planks: Roll(r, 5, 7, d), scraps: Roll(r, 5, 7, d))),
                Opt("Take the chains", "Some Scraps.", 20, (s, r, d) => Gain(s, "Heavy, but worth it.", scraps: Roll(r, 4, 5, d)))),

            () => new NightEvent("A sleeping guard", "An armoured shape slumps against the wall, breathing slow.",
                Opt("Strip its gear", "55%: rich Scraps. Otherwise it wakes - a fight.", 15, (s, r, d) =>
                    r.Next(100) < 55
                        ? Gain(s, "It never stirs.", scraps: Roll(r, 9, 12, d))
                        : new NightEventResult("Its eyes open.", startsFight: true))),

            () => new NightEvent("A kitchen for a garrison", "Iron pots and a larder the guards never finished.",
                Opt("Search the larder", "Food, and the pots are good iron.", 30, (s, r, d) => Gain(s, "Salted meat and cast iron.", food: Roll(r, 4, 6, d), scraps: Roll(r, 3, 4, d)))),
        };

        // ---------------- Anywhere ----------------
        private static readonly Func<NightEvent>[] Anywhere =
        {
            () => new NightEvent("A candlelit shrine", "Someone still tends this shrine. The candles are fresh.",
                Opt("Rest by the candles", "+25 health.", 40, (s, r, d) => Gain(s, "Warmth, and a little quiet.", health: 25)),
                Opt("Pray", "A little Hope.", 15, (s, r, d) => Gain(s, "You're not sure who's listening.", hope: 4))),

            () => new NightEvent("A fallen scavenger", "Another scavenger who didn't make it home.",
                Opt("Search the body", "Some of what they carried - maybe a remedy.", 15, (s, r, d) =>
                {
                    var result = Gain(s, "They won't need it now.", food: Roll(r, 1, 3, d), scraps: Roll(r, 1, 3, d));
                    if (r.Next(100) < 35)
                    {
                        var remedy = r.Next(2) == 0 ? Item.Bandage() : Item.Tonic();
                        return new NightEventResult($"{result.Text} A {remedy.Name} too - {s.GainItemAtNight(remedy)}.");
                    }
                    return result;
                }),
                Opt("Bury them", "Hope.", 40, (s, r, d) => Gain(s, "It's the least anyone could do.", hope: 5))),
        };

        // ---------------- The Castle: the Herald's gifts, which always cost something ----------------
        private static readonly Func<NightEvent>[] Castle =
        {
            () => new NightEvent("A sunlit chapel", "Light pours through a window that should face the night. It is warm, and it is watching.",
                Opt("Kneel in the light", "+25 health - but it feels like being seen (-4 Hope).", 20, (s, r, d) => Gain(s, "The light closes your wounds, and takes something for it.", hope: -4, health: 25)),
                Opt("Smash the window", "Lead and glass for Scraps. The light dies, and you breathe easier.", 10, (s, r, d) => Gain(s, "It shatters, and the dark comes back like cool water.", scraps: Roll(r, 3, 5, d), hope: 3))),

            () => new NightEvent("The Herald's armoury", "Racks of gilded arms, polished for a war that hasn't started yet.",
                Opt("Pry off the gilding", "Lots of Scraps, slowly.", 40, (s, r, d) => Gain(s, "Gold leaf over good steel.", scraps: Roll(r, 8, 11, d))),
                Opt("Take a flask of blessed oil", "60%: a Tonic. Otherwise it burns (-12 health).", 10, (s, r, d) =>
                {
                    if (r.Next(100) < 60)
                    {
                        var tonic = Item.Tonic();
                        return new NightEventResult($"It soothes rather than burns - a Tonic, {s.GainItemAtNight(tonic)}.");
                    }
                    return Gain(s, "It was never meant for hands like yours.", health: -12);
                }, healthRisk: 12)),

            () => new NightEvent("A choir loft", "Empty stalls. A hymn hangs in the air, sung by no one.",
                Opt("Cut the bell ropes", "Timber and cord - and no bells to raise the alarm.", 20, (s, r, d) => Gain(s, "The ropes fall slack. The hymn falters.", planks: Roll(r, 4, 6, d), hope: 2)),
                Opt("Hum along", "50%: the hymn steadies you (+8 Hope). Otherwise the choir hears you - a fight.", 5, (s, r, d) =>
                    r.Next(100) < 50
                        ? Gain(s, "For a moment it's just a song, and it's beautiful.", hope: 8)
                        : new NightEventResult("The stalls are not empty after all.", startsFight: true))),
        };

        /// <summary>A district-themed event (or, a quarter of the time, one that can happen
        /// anywhere), avoiding ones already seen tonight when possible.</summary>
        public static NightEvent Pick(District district, Random random, ISet<string> seenTonight)
        {
            var local = district switch
            {
                District.ChurchRuins => Church,
                District.CastleKeep => Keep,
                District.Castle => Castle,
                _ => Village
            };
            var pool = random.Next(100) < 25 ? Anywhere : local;

            var fresh = pool.Select(make => make()).Where(e => !seenTonight.Contains(e.Title)).ToList();
            if (fresh.Count == 0) fresh = local.Concat(Anywhere).Select(make => make()).Where(e => !seenTonight.Contains(e.Title)).ToList();
            if (fresh.Count == 0) fresh = local.Select(make => make()).ToList();

            var chosen = fresh[random.Next(fresh.Count)];
            seenTonight.Add(chosen.Title);
            return chosen;
        }
    }
}
