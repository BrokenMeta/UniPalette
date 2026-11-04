using System;

namespace BrokenMeta.UniPalette.Editor.Model
{
    internal readonly struct SearchQuery : IEquatable<SearchQuery>
    {
        private readonly string _text;

        internal static SearchQuery Empty => default;

        internal string Text => _text ?? string.Empty;

        internal bool IsEmpty => Text.Length == 0;

        internal SearchQuery(string text) =>
            _text = text ?? string.Empty;

        internal bool Matches(string text)
        {
            if (IsEmpty)
                return true;

            return text.IndexOf(Text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool Equals(SearchQuery other) =>
            string.Equals(Text, other.Text, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is SearchQuery other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(Text);

        public override string ToString() =>
            Text;
    }
}
