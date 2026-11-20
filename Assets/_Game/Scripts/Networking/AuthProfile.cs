using System.Text;

namespace BowMan.Networking
{
    // Builds Unity Authentication profile names so each running game signs in as a distinct player.
    public static class AuthProfile
    {
        private const int MaxLength = 30;

        // Returns "p<processId>[-suffix]" with only letters, digits, '-' and '_', at most 30 characters.
        public static string ForProcess(int processId, string suffix = null)
        {
            var raw = string.IsNullOrEmpty(suffix) ? $"p{processId}" : $"p{processId}-{suffix}";
            var builder = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                var allowed = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (allowed) builder.Append(c);
                if (builder.Length == MaxLength) break;
            }
            return builder.ToString();
        }
    }
}
