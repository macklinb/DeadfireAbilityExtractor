using System.Collections.Generic;

namespace DeadfireAbilityExtractor
{
    public static class Extensions
    {
        public static string ReplaceLinkText(this string text, string findStr, bool includeCaseVariations = false, string pageOverride = null)
        {
            if (string.IsNullOrEmpty(findStr) || string.IsNullOrEmpty(text))
                return text;

            if (includeCaseVariations)
            {
                // Input to lower
                text = ReplaceLinkText(text, findStr.ToLower());

                // Input to lower, but with the first character kept as original
                text = ReplaceLinkText(text, findStr.Substring(0, 1) + findStr.Substring(1).ToLower());
            }

            // findStr counter
            int x = 0;
            List<int> occurrances = new List<int>();

            // Find all occurrances using loop
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == findStr[x])
                {
                    x++;

                    // At end of found occurance
                    if (x == findStr.Length)
                    {
                        // Reset counter
                        x = 0;

                        // Ensure occurrance is a whole word (isn't bordered by an alphanumeric character)
                        if ((i - findStr.Length >= 0 && char.IsLetterOrDigit(text[i - findStr.Length])) ||
                            (i + 1 < text.Length && char.IsLetterOrDigit(text[i + 1])))
                        {

                        }

                        // Ensure occurrance doesn't start with a "[[" or end with a "]]"
                        else if ((i - findStr.Length - 1 >= 0 && text.Substring(i - findStr.Length - 1, 2) == "[[") ||
                            (i + 2 < text.Length && text.Substring(i + 1, 2) == "]]"))
                        {

                        }
                        else
                        {
                            occurrances.Add(i - findStr.Length + 1);
                        }
                    }
                }
                else
                {
                    x = 0;
                }
            }

            // Replace occurrances
            if (occurrances.Count > 0)
            {
                string replaceStr = string.Format("[[{0}{1}]]", pageOverride != null ? pageOverride + findStr + "|" : "", findStr);

                for (int i = 0; i < occurrances.Count; i++)
                {
                    int index = occurrances[i];

                    text = text.Remove(index, findStr.Length).Insert(index, replaceStr);

                    // Push back the index of other found occurrances by the length of the difference between the find string and the replace string
                    int diff = replaceStr.Length - findStr.Length;

                    for (int j = i + 1; j < occurrances.Count; j++)
                        occurrances[j] += diff;
                }
            }

            return text;
        }
    }
}
