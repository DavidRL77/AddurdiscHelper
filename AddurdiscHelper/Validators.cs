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

        public static void FilesValidator(OptionResult result)
        {
            string[] files = result.GetValueOrDefault<string[]>();
            for(int i = 0; i < files.Length; i++)
            {
                if(!File.Exists(files[i]))
                {
                    result.AddError($"{files[i]} does not exist");
                }
            }
        }
    }
}
