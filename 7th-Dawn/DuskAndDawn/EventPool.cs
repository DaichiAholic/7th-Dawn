using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public static class MorningEventPool
    {
        // Shorthand so each option below stays readable on a line or two.
        private static ResourceDelta R(int food = 0, int planks = 0, int scraps = 0, int hope = 0) =>
            new ResourceDelta(food, planks, scraps, hope);

        private static EventOption Option(string label, string resultText, ResourceDelta cost = default, ResourceDelta outcome = default) =>
            new EventOption(label, resultText, cost, outcome);

        private static EventOption Gamble(string label, int chance, ResourceDelta outcome, string resultText, ResourceDelta failOutcome, string failText, ResourceDelta cost = default) =>
            new EventOption(label, resultText, cost, outcome, chance, failOutcome, failText);

        // Every event keeps at least one option with no Cost, so there's always something
        // the player can pick. Refusing often isn't free, though - turning people away
        // costs Hope.
        public static readonly List<MorningEvent> All = new List<MorningEvent>
        {
            // ---- Trades ----
            new MorningEvent("A trader passes by", "A peddler with a handcart offers dried meat and hard bread.", new[]
            {
                Option("Trade scraps for food", "You swap scrap metal for a sack of bread.", cost: R(scraps: 5), outcome: R(food: 8)),
                Option("Trade planks for food", "He straps your planks to his cart and leaves the food.", cost: R(planks: 5), outcome: R(food: 7)),
                Option("Send them on their way", "The trader shrugs and trundles off.")
            }),

            new MorningEvent("Scavengers need food", "A hungry crew offers building materials for a hot meal.", new[]
            {
                Option("Feed them for planks", "They eat fast and leave a stack of timber behind.", cost: R(food: 6), outcome: R(planks: 10)),
                Option("Feed them for scraps", "They pay in bent nails and hinges.", cost: R(food: 6), outcome: R(scraps: 9)),
                Option("Refuse", "They leave muttering. The house feels a little colder.", outcome: R(hope: -2))
            }),

            new MorningEvent("A merchant caravan", "Three wagons pass under heavy guard, willing to trade.", new[]
            {
                Option("Buy salted meat", "A barrel of salted meat rolls into the larder.", cost: R(scraps: 6), outcome: R(food: 10)),
                Option("Sell them food", "They pay well for fresh bread.", cost: R(food: 6), outcome: R(planks: 6, scraps: 5)),
                Option("Wave them on", "The wagons creak away down the road.")
            }),

            new MorningEvent("A scrap dealer", "A crooked little man with a cart of rusted metal.", new[]
            {
                Option("Trade planks", "He weighs the planks twice, then pays up.", cost: R(planks: 6), outcome: R(scraps: 6)),
                Option("Trade food", "He eats half of it before he's out the gate.", cost: R(food: 5), outcome: R(scraps: 7)),
                Option("No deal", "He spits and wheels his cart away.")
            }),

            new MorningEvent("A risky deal", "A masked dealer offers good timber - but the price is steep and the company worse.", new[]
            {
                Option("Take the deal", "The timber is good. Nobody sleeps well tonight.", cost: R(scraps: 8, hope: 5), outcome: R(planks: 15)),
                Option("Walk away", "You bar the door behind them.")
            }),

            // ---- People at the gate (Food for Hope) ----
            new MorningEvent("A stranger asks for shelter", "A thin figure at the gate begs for a place by the fire.", new[]
            {
                Option("Take them in", "A new face at the table. Spirits lift.", cost: R(food: 4), outcome: R(hope: 7)),
                Option("Turn them away", "You watch them walk back into the dark.", outcome: R(hope: -4))
            }),

            new MorningEvent("Survivors at the gate", "A family arrives with what they could carry. They're starving.", new[]
            {
                Option("Feed them", "They share what they brought, grateful to eat.", cost: R(food: 8), outcome: R(planks: 5, scraps: 5, hope: 6)),
                Option("Give them a little", "It isn't much, but they thank you.", cost: R(food: 3), outcome: R(hope: 3)),
                Option("Turn them away", "The children's faces stay with everyone all day.", outcome: R(hope: -6))
            }),

            new MorningEvent("A wandering preacher", "An old man in torn robes offers words of comfort for a meal.", new[]
            {
                Option("Share a meal", "He speaks of a dawn that doesn't burn. People listen.", cost: R(food: 3), outcome: R(hope: 6)),
                Option("Pay for a blessing", "He blesses every doorway. It helps, a little.", cost: R(scraps: 4), outcome: R(hope: 5)),
                Option("Close the door", "His muttered prayers fade down the road.")
            }),

            // ---- The household (Food and Hope) ----
            new MorningEvent("Festival of the Seventh Dawn", "The children want to mark another sunrise survived.", new[]
            {
                Option("Hold a feast", "Singing, full plates, and for once, laughter.", cost: R(food: 8), outcome: R(hope: 10)),
                Option("A small celebration", "A few sweet cakes and a song.", cost: R(food: 3), outcome: R(hope: 6)),
                Option("Not today", "The children go quiet and drift back inside.", outcome: R(hope: -3))
            }),

            new MorningEvent("A night of bad dreams", "Everyone woke screaming about the holy light.", new[]
            {
                Option("Warm milk and bread", "Full bellies settle frayed nerves.", cost: R(food: 4), outcome: R(hope: 6)),
                Option("Shake it off", "Nobody quite manages to.", outcome: R(hope: -5))
            }),

            new MorningEvent("Despair creeps in", "Nobody speaks at breakfast. Some stare at the door.", new[]
            {
                Option("Stories by the fire", "Old tales and a warm meal pull everyone back together.", cost: R(food: 3), outcome: R(hope: 7)),
                Option("Let them be", "The silence settles in like damp.", outcome: R(hope: -4))
            }, state => state.Hope < 45),

            new MorningEvent("Empty stomachs", "The stores are nearly bare and tempers are short.", new[]
            {
                Option("Boil old leather", "It's barely food, but it's something.", cost: R(scraps: 3), outcome: R(food: 4)),
                Option("Tighten belts", "Everyone goes to bed hungry.", outcome: R(hope: -6))
            }, state => state.Food < state.DailyUpkeep),

            // ---- Mishaps ----
            new MorningEvent("Rats in the larder", "Something has been gnawing at the food stores overnight.", new[]
            {
                Option("Throw out the spoiled food", "You scrape the ruined food into the fire.", outcome: R(food: -5)),
                Gamble("Eat it anyway", 50, default, "It tastes foul, but nobody falls ill.",
                    R(hope: -10), "Half the house is sick by noon.")
            }, state => state.Food >= 5),

            new MorningEvent("The roof is leaking", "Last night's rain found every crack in the ceiling.", new[]
            {
                Option("Patch it properly", "Dry again, and sturdier than before.", cost: R(planks: 5), outcome: R(hope: 2)),
                Option("Leave it", "Water soaks the stores and everyone's bedding.", outcome: R(food: -3, hope: -4))
            }),

            // ---- Gambles ----
            new MorningEvent("Foraging party", "A few of the household want to search the nearby fields while the light is gentle.", new[]
            {
                Gamble("Send them out", 70, R(food: 7), "They come back with baskets of roots and berries.",
                    R(hope: -6), "One comes back hurt and empty-handed."),
                Option("Too risky", "They grumble, but stay inside.")
            }),

            new MorningEvent("Debris in the river", "The current has washed wreckage up against the old wall.", new[]
            {
                Gamble("Haul it in", 80, R(planks: 6, scraps: 3), "Good timber and a tangle of iron.",
                    R(hope: -3), "The bank gives way. Everyone gets soaked, and nothing's saved."),
                Option("Leave it", "The river carries it off by noon.")
            }),

            // ---- Good days ----
            new MorningEvent("The garden blooms", "The Kitchen's herb beds came up overnight.", new[]
            {
                Option("Harvest it all", "Baskets of greens for the larder.", outcome: R(food: 6)),
                Option("Share with the neighbours", "They send back thanks, and a promise to return the favour.", outcome: R(food: 3, hope: 6))
            }, state => state.Level(BaseRoomType.Kitchen) >= 2),

            new MorningEvent("Quiet morning", "Nothing stirs. For once, the house simply rests.", new[]
            {
                Option("Enjoy the quiet", "A slow breakfast, and nobody in a hurry.", outcome: R(hope: 3))
            })
        };

        /// <summary>A random event that can happen today, avoiding yesterday's if possible.</summary>
        public static MorningEvent GetRandom(Random random, PlayerState state)
        {
            var eligible = All.Where(e => e.CanHappen(state) && e.Title != state.LastMorningEventTitle).ToList();
            if (eligible.Count == 0)
            {
                eligible = All.Where(e => e.CanHappen(state)).ToList();
            }
            return eligible[random.Next(eligible.Count)];
        }
    }
}
