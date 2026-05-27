using AddurdiscHelper.Model;
using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddurdiscHelper
{
    internal static class Parsers
    {
        public static ColorRange[] ColorRangeParser(ArgumentResult result)
        {
            List<ColorRange> colorRanges = new();

            // "0-255,10" -> ColorRange { R: 0-255, G: 10-10, B: 10-10 }
            for(int i = 0; i < result.Tokens.Count; i++)
            {
                string[] splitRGB = result.Tokens[i].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if(splitRGB.Length == 0)
                {
                    result.AddError("No colors provided");
                    continue;
                }

                Model.Range[] ranges = new Model.Range[3];
                for(int j = 0; j < ranges.Length; j++)
                {
                    // If less than three values were provided, use the previous value to fill the rest of the ranges
                    if(j >= splitRGB.Length)
                    {
                        Array.Fill(ranges, ranges[j - 1], j, ranges.Length - j);
                        break;
                    }

                    string[] splitRange = splitRGB[j].Split('-');
                    if(splitRange.Length > 2)
                    {
                        result.AddError("Range can only have two values");
                        break;
                    }

                    Model.Range range = new Model.Range(0, 0);
                    for(int k = 0; k < splitRange.Length; k++)
                    {
                        string num = splitRange[k];
                        if(!int.TryParse(num, out int parsed))
                        {
                            result.AddError("Please provide numbers");
                            break;
                        }

                        if(k == 0)
                        {
                            range.Min = parsed;
                        }

                        // If there's only one number in the range, use that one for upper and lower bounds
                        if(k == 1 || k == splitRange.Length - 1)
                        {
                            range.Max = parsed;
                        }
                    }

                    // Swap the bounds if the user is stupid (I'm user)
                    if(range.Min > range.Max)
                    {
                        (range.Min, range.Max) = (range.Max, range.Min);
                    }

                    ranges[j] = range;
                }

                colorRanges.Add(new ColorRange(ranges[0], ranges[1], ranges[2]));
            }

            return colorRanges.ToArray();
        }
    }
}
