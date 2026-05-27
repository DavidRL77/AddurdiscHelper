using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddurdiscHelper
{
    internal static class Validators
    {
        public static void DirectoryValidator(OptionResult result)
        {
            string value = result.GetValueOrDefault<string>();
            if(!Directory.Exists(value))
            {
                result.AddError($"Directory '{value}' does not exist");
            }
        }
    }
}
