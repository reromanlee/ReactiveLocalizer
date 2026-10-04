using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// Turns localization names into C# identifiers. Every valid name already is one, except the C# keywords, which
    /// get the <c>@</c> prefix C# offers for exactly this.
    /// </summary>
    internal static class CSharpNames
    {
        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
            "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out",
            "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try",
            "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        /// <summary>Returns <paramref name="name"/> as an identifier, with <c>@</c> in front of a keyword.</summary>
        public static string ToIdentifier(string name) => Keywords.Contains(name) ? "@" + name : name;

        /// <summary>Returns whether <paramref name="name"/> is a dotted namespace whose every part is a valid identifier.</summary>
        public static bool IsValidNamespace(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            string[] parts = name.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || Keywords.Contains(parts[i]) || !IsIdentifier(parts[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsIdentifier(string part)
        {
            char first = part[0];
            if (!char.IsLetter(first) && first != '_')
            {
                return false;
            }
            for (int i = 1; i < part.Length; i++)
            {
                if (!char.IsLetterOrDigit(part[i]) && part[i] != '_')
                {
                    return false;
                }
            }
            return true;
        }
    }
}
