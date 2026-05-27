using AddurdiscHelper.Model;
using SkiaSharp;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Drawing;
using System.Numerics;
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

            Command renameCommand = new("rename", "Renames files in the input dir to remove any illegal characters, copying them to the output dir.");
            Command texturesCommand = new("textures", "Generates randomly colored disc textures for each audio file.") 
            { 
                filterOption,
                seedOption
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
                GenerateTextures(input, output, filter, 0, new LayerInfo("textures/layer0.png", new ColorRange()), 
                    new LayerInfo("textures/layer1.png", new ColorRange()));
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
                byte[] colors = new byte[3];
                rnd.NextBytes(colors);

                SKColor color = new SKColor(colors[0], colors[1], colors[2]);

            // Actual creation of the output image
            using(Stream file = File.OpenRead(layers[0].Path))
            using(SKBitmap bitmap = SKBitmap.Decode(file))
            using(SKSurface surface = SKSurface.Create(bitmap.Info))
            using(SKPaint paint = new SKPaint() { ColorFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.Modulate) })
            {
                surface.Canvas.DrawBitmap(bitmap, 0, 0, paint);

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
    }
}
