using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace FastProxy.App
{
    internal static class ParseUtils
    {
        public static bool ParseArguments<TShared, TVerb>(string[] args, out TShared options, out TVerb verbOptions)
        {
            var verbTypes = typeof(Options).Assembly
                .GetTypes()
                .Where(p => typeof(TVerb).IsAssignableFrom(p) && p.GetCustomAttributes(typeof(VerbAttribute), true).Length > 0)
                .ToList();

            var verbs = verbTypes.Select(p => ((VerbAttribute)p.GetCustomAttributes(typeof(VerbAttribute), true)[0]).Name).ToArray();
            var sharedArgs = new List<string>();
            var verbArgs = new List<string>();

            SplitArgs(args, verbs, sharedArgs, verbArgs);

            options = default;
            verbOptions = default;

            if (!(Parser.Default.ParseArguments<TShared>(sharedArgs) is Parsed<TShared> parsedOptions))
                return false;
            if (!(Parser.Default.ParseArguments(verbArgs, verbTypes.ToArray()) is Parsed<object> parsedVerbOptions))
                return false;

            options = parsedOptions.Value;
            verbOptions = (TVerb)parsedVerbOptions.Value;

            return true;
        }

        private static void SplitArgs(string[] args, string[] verbs, List<string> sharedArgs, List<string> verbArgs)
        {
            bool hadVerb = false;

            foreach (string arg in args)
            {
                if (!hadVerb && ((IList)verbs).Contains(arg))
                    hadVerb = true;
                (hadVerb ? verbArgs : sharedArgs).Add(arg);
            }
        }

        public static int? ParseSize(string size)
        {
            if (size == null)
                return null;

            size = size.Trim();
            int multiplier = 1;

            if (
                TryParseSize("K", 1024) ||
                TryParseSize("M", 1024 * 1024) ||
                TryParseSize("G", 1024 * 1024 * 1024)
            )
                size = size.Trim();

            return int.Parse(size) * multiplier;

            bool TryParseSize(string postfix, int value)
            {
                if (size.EndsWith(postfix, StringComparison.OrdinalIgnoreCase))
                {
                    multiplier = value;
                    size = size.Substring(0, size.Length - 1);
                    return true;
                }
                return false;
            }
        }
    }
}
