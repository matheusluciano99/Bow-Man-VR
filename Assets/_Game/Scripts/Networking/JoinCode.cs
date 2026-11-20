using System.Text;

namespace BowMan.Networking
{
    // Cleans and checks Relay join codes typed by a player.
    public static class JoinCode
    {
        private const int MinLength = 6;
        private const int MaxLength = 12;

        // Removes whitespace and upper-cases the code so casual typing still matches.
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var builder = new StringBuilder(raw.Length);
            foreach (var c in raw)
                if (!char.IsWhiteSpace(c)) builder.Append(char.ToUpperInvariant(c));
            return builder.ToString();
        }

        // Accepts 6 to 12 ASCII letters and digits.
        public static bool IsValid(string normalized)
        {
            if (string.IsNullOrEmpty(normalized) || normalized.Length < MinLength || normalized.Length > MaxLength)
                return false;
            foreach (var c in normalized)
            {
                var letter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                if (!letter && !(c >= '0' && c <= '9')) return false;
            }
            return true;
        }
    }
}
