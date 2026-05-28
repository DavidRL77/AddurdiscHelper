using AddurdiscHelper.Extensions;
using AddurdiscHelper.Model;
using SkiaSharp;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            inputOption.Validators.Add(Validators.DirectoryValidator);

            Option<string> outputOption = new("--output", "-o")
            {
                Description = "Where to place the resulting files [default: <input>]",
                DefaultValueFactory = r => r.GetValue(inputOption)!,
                Recursive = true
            };
            outputOption.Validators.Add(Validators.DirectoryValidator);

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
                CustomParser = Parsers.ColorRangeParser,
                AllowMultipleArgumentsPerToken = true
            };

            Option<string[]> layersOption = new("--layers")
            {
                Description = "What layers to use to create the disc textures",
                AllowMultipleArgumentsPerToken = true,
                DefaultValueFactory = r => ["layers/layer0.png", "layers/layer1.png"],
                Arity = ArgumentArity.OneOrMore,
            };
            layersOption.Validators.Add(Validators.FilesValidator);

            Option<int> groupOption = new("--group", "-g")
            {
                Description = "How many characters of the file's name will be used to group equals by color.",
                DefaultValueFactory = r => 0
            };

            Option<bool> overwriteOption = new("--overwrite")
            {
                Description = "Overwrite existing files (not recommended)",
                DefaultValueFactory = r => false,
                Recursive = true
            };

            Option<string> texturePrefixOption = new("--prefix", "-p")
            {
                Description = "Prefix to add to texture names",
                DefaultValueFactory = r => "disc_",
                Recursive = true
            };

            Option<string> langPrefixOption = new("--prefix", "-p")
            {
                Description = "Prefix to add to the item's id",
                DefaultValueFactory = r => @"item.addurdisc.disc_"
            };

            Command renameCommand = new("rename", "Renames files in the input dir to remove any illegal characters, copying them to the output dir.");
            Command texturesCommand = new("textures", "Generates randomly colored disc textures for each audio file.") 
            { 
                filterOption,
                seedOption,
                colorRangesOption,
                layersOption,
                groupOption,
                texturePrefixOption
            };
            Command langCommand = new("lang", "Generates lang file with proper capitalization.")
            {
                langPrefixOption
            };

            RootCommand cmd = new("A little helper tool to generate the proper files for the minecraft mod 'addurdisc'")
            {
                inputOption,
                outputOption,
                overwriteOption,
                renameCommand,
                texturesCommand,
                langCommand
            };

            renameCommand.SetAction(parseResult =>
            {
                string input = parseResult.GetValue(inputOption)!;
                string output = parseResult.GetValue(outputOption)!;
                bool overwrite = parseResult.GetValue(overwriteOption)!;
                RenameFiles(input, output, overwrite);
            });

            texturesCommand.SetAction(parseResult =>
            {
                string input = parseResult.GetValue(inputOption)!;
                string output = parseResult.GetValue(outputOption)!;
                bool overwrite = parseResult.GetValue(overwriteOption)!;
                string filter = parseResult.GetValue(filterOption)!;
                string prefix = parseResult.GetValue(texturePrefixOption)!;
                int seed = parseResult.GetValue(seedOption)!;
                int groupLength = parseResult.GetValue(groupOption)!;
                ColorRange[] ranges = parseResult.GetValue(colorRangesOption)!;
                string[] layerPaths = parseResult.GetValue(layersOption)!;

                List<LayerInfo> layerInfos = new List<LayerInfo>();
                for(int i = 0; i < layerPaths.Length; i++)
                {
                    string path = layerPaths[i];
                    ColorRange range = ranges.ElementAtOrDefault(i) ?? new ColorRange();
                    layerInfos.Add(new LayerInfo(path, range));
                }

                GenerateTextures(input, output, overwrite, filter, prefix, seed, groupLength, layerInfos.ToArray());
            });

            langCommand.SetAction(parseResult =>
            {
                string input = parseResult.GetValue(inputOption)!;
                string output = parseResult.GetValue(outputOption)!;
                string prefix = parseResult.GetValue(langPrefixOption)!;
                bool overwrite = parseResult.GetValue(overwriteOption)!;
                GenerateLang(input, output, prefix, overwrite);
            });

            return cmd.Parse(args).Invoke();
        }

        private static int RenameFiles(string inputDir, string outputDir, bool overwrite)
        {
            // No need to check if they exist, the validator takes care of that
            DirectoryInfo inputInfo = new DirectoryInfo(inputDir);
            DirectoryInfo outputInfo = new DirectoryInfo(outputDir);

            foreach(FileInfo file in inputInfo.EnumerateFiles("*.ogg"))
            {
                Console.WriteLine(file.Name);
                string cleanName = Path.GetFileNameWithoutExtension(file.Name).Replace(" ", "_").ToLower();
                cleanName = IllegalChars.Replace(cleanName, "");

                string targetPath = Path.Combine(outputInfo.FullName, cleanName + ".ogg");
                if(File.Exists(targetPath) && !overwrite)
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

        private static int GenerateTextures(string inputDir, string outputDir, bool overwrite, string filter, string prefix, int seed, int groupLength, params LayerInfo[] layers)
        {
            DirectoryInfo inputInfo = new DirectoryInfo(inputDir);
            DirectoryInfo outputInfo = new DirectoryInfo(Path.Combine(outputDir, "textures"));
            if(!outputInfo.Exists) outputInfo.Create();

            Regex regex = new Regex(filter);

            foreach(FileInfo file in inputInfo.EnumerateFiles("*.ogg"))
            {
                if(!regex.IsMatch(file.Name)) continue;

                string nameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
                FileInfo outputFile = new FileInfo(Path.Combine(outputInfo.FullName, prefix + nameWithoutExtension + ".png"));
                if(outputFile.Exists && !overwrite)
                {
                    Console.WriteLine($"Skipping {outputFile.FullName}, already exists...");
                    continue;
                }

                // Random color generation
                int fullHash = file.Name.GetStableHashCode();
                int groupHash = groupLength > 0 ? file.Name.Substring(0, groupLength).GetStableHashCode() : fullHash;

                int groupSeed = HashExtensions.Combine(groupHash, seed);
                int fullSeed = HashExtensions.Combine(fullHash, seed);

                byte[] imageBytes = CreateImage(groupSeed, fullSeed, layers);
                File.WriteAllBytes(outputFile.FullName, imageBytes);
            }

            return 0;
        }

        private static void GenerateLang(string inputDir, string outputDir, string idPrefix, bool overwrite)
        {
            DirectoryInfo inputInfo = new DirectoryInfo(inputDir);
            DirectoryInfo outputInfo = new DirectoryInfo(Path.Combine(outputDir, "lang"));
            if(!outputInfo.Exists) outputInfo.Create();

            string outputFile = Path.Combine(outputInfo.FullName, "en_us.json");
            if(File.Exists(outputFile) && !overwrite)
            {
                Console.Error.WriteLine($"Skipping {outputFile}, already exists...");
                return;
            }

            Dictionary<string, string> langDict = new();
            foreach(FileInfo file in inputInfo.EnumerateFiles("*.ogg"))
            {
                string name = Path.GetFileNameWithoutExtension(file.Name);
                string id = idPrefix + name;
                string descId = id + ".desc";
                langDict.Add(id, "Music Disc");

                string prettyName = name.Replace("_", " ").ToTitleCase();
                langDict.Add(descId, prettyName);
            }

            string json = JsonSerializer.Serialize(langDict, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(outputFile, json);
        }

        private static byte[] CreateImage(int groupSeed, int seed, LayerInfo[] layers)
        {
            // The base of the texture needs to be the size of layer0
            using(Stream layer0Stream = File.OpenRead(layers[0].Path))
            using(SKBitmap layer0Bitmap = SKBitmap.Decode(layer0Stream))
            using(SKSurface surface = SKSurface.Create(layer0Bitmap.Info))
            {
                // Paint every layer on top of our surface with its random color
                for(int i = 0; i < layers.Length; i++)
                {
                    // Layer 0 is treated as the "groupable" layer, that is, if the group seed is the same across multiple images,
                    // layer 0 will always generate the same color, while every other layer has a unique seed.
                    // Even if the group seed and normal seed have the same value, each layer above 0 ensures it has a unique seed.
                    int layerSeed = i == 0 ? groupSeed : HashExtensions.Combine(seed, i);

                    Random rnd = new Random(layerSeed);

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
    }

}
