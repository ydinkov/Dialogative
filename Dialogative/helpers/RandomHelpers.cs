using System;
using System.Collections.Generic;

namespace Dialogative.helpers
{
    internal static class RandomHelpers
    {
        private static Random rng = new();

        internal static string Choice(this IList<string> me, Func<ICollection<string>, string>? randomChooser = null)
        {
            if (randomChooser is null)
            {
                var max = me.Count - 1;
                var random = rng.Next(max);
                return me[random];
            }
            else
            {
                return randomChooser(me);
            }
        }
    }
}