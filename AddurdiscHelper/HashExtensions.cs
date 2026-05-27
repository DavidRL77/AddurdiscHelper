namespace AddurdiscHelper
{
    // Most of this is magic I won't even pretend to understand
    internal static class HashExtensions
    {
        // Source - https://stackoverflow.com/a/36845864
        // Posted by Scott Chamberlain, modified by community. See post 'Timeline' for change history
        // Retrieved 2026-05-27, License - CC BY-SA 4.0

        public static int GetStableHashCode(this string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for(int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if(i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        public static int Combine(int a, int b)
        {
            // Source - https://stackoverflow.com/a/1646913
            // Posted by Jon Skeet, modified by community. See post 'Timeline' for change history
            // Retrieved 2026-05-27, License - CC BY-SA 3.0

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + a;
                hash = hash * 31 + b;
                return hash;
            }

        }
    }
}
