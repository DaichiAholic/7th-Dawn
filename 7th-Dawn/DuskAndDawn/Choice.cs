using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class ChoiceOption
    {
        public string Title { get; }
        public string Description { get; }

        private readonly Func<PlayerState, Random, string> _resolve;

        public ChoiceOption(string title, string description, Func<PlayerState, Random, string> resolve)
        {
            Title = title;
            Description = description;
            _resolve = resolve;
        }

        public string Resolve(PlayerState state, Random random) => _resolve(state, random);
    }
}
