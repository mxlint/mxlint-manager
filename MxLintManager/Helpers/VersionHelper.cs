using System;
using System.Collections.Generic;
using System.Text;

namespace MxLintManager.Helpers
{
    public static class VersionHelper
    {
        public static int Compare(string a, string b) => Parse(a).CompareTo(Parse(b));

        public static Version Parse(string value) =>
            Version.TryParse(value?.Trim().TrimStart('v', 'V'), out var v)
                ? v
                : new Version(0, 0, 0);
    }
}
