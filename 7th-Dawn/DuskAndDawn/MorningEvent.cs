using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public readonly struct ResourceDelta
    {
        public int Food { get; }
        public int Planks { get; }
        public int Scraps { get; }
        public int Hope { get; }

        public ResourceDelta(int food = 0, int planks = 0, int scraps = 0, int hope = 0)
        {
            Food = food;
            Planks = planks;
            Scraps = scraps;
            Hope = hope;
        }

        public bool IsEmpty => Food == 0 && Planks == 0 && Scraps == 0 && Hope == 0;

        public static ResourceDelta operator -(ResourceDelta d) => new ResourceDelta(-d.Food, -d.Planks, -d.Scraps, -d.Hope);

        public static ResourceDelta operator +(ResourceDelta a, ResourceDelta b) =>
            new ResourceDelta(a.Food + b.Food, a.Planks + b.Planks, a.Scraps + b.Scraps, a.Hope + b.Hope);

        public IEnumerable<(string text, bool gain)> Parts()
        {
            if (Food != 0) yield return (Signed(Food) + " Food", Food > 0);
            if (Planks != 0) yield return (Signed(Planks) + " Planks", Planks > 0);
            if (Scraps != 0) yield return (Signed(Scraps) + " Scraps", Scraps > 0);
            if (Hope != 0) yield return (Signed(Hope) + " Hope", Hope > 0);
        }

        public string Describe() => IsEmpty ? "Nothing" : string.Join(", ", Parts().Select(p => p.text));

        private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
    }

    public class EventOption
    {
        public string Label { get; }
        public ResourceDelta Cost { get; }
        public ResourceDelta Outcome { get; }
        public string ResultText { get; }
        public int Chance { get; }
        public ResourceDelta FailOutcome { get; }
        public string FailText { get; }

        public EventOption(string label, string resultText,
            ResourceDelta cost = default, ResourceDelta outcome = default,
            int chance = 100, ResourceDelta failOutcome = default, string failText = null)
        {
            Label = label;
            ResultText = resultText;
            Cost = cost;
            Outcome = outcome;
            Chance = chance;
            FailOutcome = failOutcome;
            FailText = failText ?? resultText;
        }

        public bool IsGamble => Chance < 100;

        public bool CanAfford(PlayerState state) =>
            state.CanAfford(Cost.Food, Cost.Planks, Cost.Scraps) && (Cost.Hope <= 0 || state.Hope > Cost.Hope);

        public (string text, ResourceDelta applied) Resolve(PlayerState state, Random random)
        {
            var paid = state.Apply(-Cost);
            bool success = random.Next(100) < Chance;
            var result = state.Apply(success ? Outcome : FailOutcome);
            return (success ? ResultText : FailText, paid + result);
        }
    }

    public class MorningEvent
    {
        public string Title { get; }
        public string Description { get; }
        public IReadOnlyList<EventOption> Options { get; }

        private readonly Func<PlayerState, bool> _condition;

        public MorningEvent(string title, string description, IReadOnlyList<EventOption> options, Func<PlayerState, bool> condition = null)
        {
            Title = title;
            Description = description;
            Options = options;
            _condition = condition;
        }

        public bool CanHappen(PlayerState state) => _condition == null || _condition(state);
    }
}
