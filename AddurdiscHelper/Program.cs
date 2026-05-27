using AddurdiscHelper.Model;
using SkiaSharp;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.RegularExpressions;

namespace AddurdiscHelper
{
    internal class Program
    {
        private readonly static Regex IllegalChars = new Regex("[^a-zA-Z0-9_\\-]");

        static int Main(string[] args)
        {
            Option<string> inputOption = new("--input", "-i")
            {
                Description = "The directory where the audio files are located",
                DefaultValueFactory = r => Directory.GetCurrentDirectory(),
                Recursive = true
            };
            inputOption.Validators.Add(DirectoryValidator);

            Option<string> outputOption = new("--output", "-o")
            {
                Description = "Where to place the resulting files [default: <input>]",
                DefaultValueFactory = r => r.GetValue(inputOption)!,
                Recursive = true
            };
            outputOption.Validators.Add(DirectoryValidator);

            Option<string> filterOption = new("--filter", "-f")
            {
                Description = "Regex filter to only generate textures for files that match it.",
                DefaultValueFactory = r => ".+"
            };

            Option<int> seedOption = new("--seed", "-s")
            {
                Description = "Seed to use for the color generation of the textures",
                DefaultValueFactory = r => 0
            };

            Option<ColorRange[]> colorRangesOption = new("--color-ranges")
            {
                Description = "Color range for each layer. (Possible formats: 0-255,0-255,0-255 or 0-255 or 0,0,0 or 0)",
                CustomParser = ColorRangeParser,
                AllowMultipleArgumentsPerToken = true
            };

            Command renameCommand = new("rename", "Renames files in the input dir to remove any illegal characters, copying them to the output dir.");
            Command texturesCommand = new("textures", "Generates randomly colored disc textures for each audio file.") 
            { 
                filterOption,
                seedOption,
                colorRangesOption
            };
            RootCommand cmd = new("A little helper tool to generate the proper files for the mod 'addurdisc'")
            {
                inputOption,
                outputOption,
                renameCommand,
                texturesCommand
            };

            renameCommand.SetAction(parseResult =>
            {
                string input = parseResult.GetValue(inputOption)!;
                string output = parseResult.GetValue(outputOption)!;
                RenameFiles(input, output);
            });

            texturesCommand.SetAction(parseResult =>
            {
                string input = parseResult.GetValue(inputOption)!;
                string output = parseResult.GetValue(outputOption)!;
                string filter = parseResult.GetValue(filterOption)!;
                ColorRange[] ranges = parseResult.GetValue(colorRangesOption)!;

                GenerateTextures(input, output, filter, 0, new LayerInfo("textures/layer0.png", ranges.ElementAtOrDefault(0) ?? new ColorRange()), 
                    new LayerInfo("textures/layer1.png", ranges.ElementAtOrDefault(1) ?? new ColorRange()));
            });

            return cmd.Parse(args).Invoke();
        }

        private static int RenameFiles(string inputDir, string outputDir)
        {
            // No need to check if they exist, the validator takes care of that
            DirectoryInfo inputInfo = new DirectoryInfo(inputDir);
            DirectoryInfo outputInfo = new DirectoryInfo(outputDir);

            foreach(FileInfo file in inputInfo.EnumerateFiles("*.ogg"))
            {
                string cleanName = Path.GetFileNameWithoutExtension(file.Name).Replace(" ", "_").ToLower();
                cleanName = IllegalChars.Replace(cleanName, "");

                string targetPath = Path.Combine(outputInfo.FullName, cleanName + ".ogg");
                if(File.Exists(targetPath))
                {
                    Console.WriteLine($"Skipping {targetPath}, already renamed...");
                    continue;
                }

                if(inputInfo.FullName == outputInfo.FullName)
                    file.MoveTo(targetPath);
                else
                    file.CopyTo(targetPath);
            }

            return 0;
        }

        private static int GenerateTextures(string inputDir, string outputDir, string filter, int seed, params LayerInfo[] layers)
        {
            DirectoryInfo inputInfo = new DirectoryInfo(inputDir);
            DirectoryInfo outputInfo = new DirectoryInfo(Path.Combine(outputDir, "textures"));
            if(!outputInfo.Exists) outputInfo.Create();

            Regex regex = new Regex(filter);

            foreach(FileInfo file in inputInfo.EnumerateFiles())
            {
                if(!regex.IsMatch(file.Name)) continue;

                string nameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
                FileInfo outputFile = new FileInfo(Path.Combine(outputInfo.FullName, nameWithoutExtension + ".png"));
                if(outputFile.Exists)
                {
                    Console.WriteLine($"Skipping {outputFile.FullName}, already exists...");
                    continue;
                }

                // Random color generation
                int fileSeed = file.Name.GetHashCode() + seed;
                byte[] imageBytes = CreateImage(fileSeed, layers);
                File.WriteAllBytes(outputFile.FullName, imageBytes);
            }

            return 0;
        }

        private static byte[] CreateImage(int seed, LayerInfo[] layers)
        {
            Random rnd = new Random(seed);

            // The base of the texture needs to be the size of layer0
            using(Stream layer0Stream = File.OpenRead(layers[0].Path))
            using(SKBitmap layer0Bitmap = SKBitmap.Decode(layer0Stream))
            using(SKSurface surface = SKSurface.Create(layer0Bitmap.Info))
            {
                // Paint every layer on top of our surface with its random color
                for(int i = 0; i < layers.Length; i++)
                {
                    LayerInfo layer = layers[i];

                    SKColor color = new SKColor(
                       (byte)layer.ColorRange.R.GetRandom(rnd),
                       (byte)layer.ColorRange.G.GetRandom(rnd),
                       (byte)layer.ColorRange.B.GetRandom(rnd));

                    using(SKPaint paint = new SKPaint() { ColorFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.Modulate) })
                    {
                        // Avoid creating a duplicate bitmap
                        if(i == 0)
                        {
                            surface.Canvas.DrawBitmap(layer0Bitmap, 0, 0, paint);
                            continue;
                        }

                        using(Stream layerStream = File.OpenRead(layer.Path))
                        using(SKBitmap layerBitmap = SKBitmap.Decode(layerStream))
                        {
                            surface.Canvas.DrawBitmap(layerBitmap, 0, 0, paint);
                        }
                    }
                    
                }

                using(SKImage image = surface.Snapshot())
                using(SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    return data.ToArray();
                }
            }
        } 

        private static void DirectoryValidator(OptionResult result)
        {
            string value = result.GetValueOrDefault<string>();
            if(!Directory.Exists(value))
            {
                result.AddError($"Directory '{value}' does not exist");
            }
        }

        private static ColorRange[] ColorRangeParser(ArgumentResult result)
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
                        Array.Fill(ranges, ranges[j - 1], j, ranges.Length-j);
                        break;
                    }

                    string[] splitRange = splitRGB[j].Split('-');
                    if(splitRange.Length > 2)
                    {
                        result.AddError("Range can only have two values");
                        break;
                    }

                    Model.Range range = new Model.Range(0,0);
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
