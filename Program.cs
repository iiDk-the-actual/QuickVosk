using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using NAudio.Wave;
using Vosk;
using Newtonsoft.Json.Linq;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 1 || args[0] == "-?" || args[0] == "--help")
        {
            Console.WriteLine("Usage: QuickVosk.exe <path-to-audio.wav>");
            Console.WriteLine("Note: WAV must be mono, 16-bit PCM, 16kHz.");
            return;
        }

        Console.SetError(TextWriter.Null);
        Vosk.Vosk.SetLogLevel(0);

        string audioPath = args[0];
        if (!File.Exists(audioPath))
        {
            Console.WriteLine("File not found");
            return;
        }

        string tempModelDir = Path.GetTempPath();
        if (!Directory.Exists(Path.Combine(Path.GetTempPath(), "vosk-model-small-en-us-0.15")) || !Directory.EnumerateFiles(tempModelDir).Any())
            ExtractEmbeddedZip("QuickVosk.Resources.vosk-model-small-en-us-0.15_zip", tempModelDir);

        Vosk.Vosk.SetLogLevel(0);
        using var model = new Model(Path.Combine(Path.GetTempPath(), "vosk-model-small-en-us-0.15"));
        using var waveReader = new WaveFileReader(audioPath);

        if (waveReader.WaveFormat.Encoding != WaveFormatEncoding.Pcm ||
            waveReader.WaveFormat.BitsPerSample != 16 ||
            waveReader.WaveFormat.Channels != 1)
        {
            Console.WriteLine("Could not transcribe audio");
            return;
        }

        using var rec = new VoskRecognizer(model, waveReader.WaveFormat.SampleRate);
        rec.SetWords(true);

        byte[] buffer = new byte[4096];
        int bytesRead;

        while ((bytesRead = waveReader.Read(buffer, 0, buffer.Length)) > 0)
            rec.AcceptWaveform(buffer, bytesRead);

        string result = rec.FinalResult();
        Console.WriteLine(JObject.Parse(result)["text"]);
    }

    static void ExtractEmbeddedZip(string resourceName, string outputDir)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Console.WriteLine("Failed to load vosk model");
            return;
        }

        using var archive = new ZipArchive(stream);
        foreach (var entry in archive.Entries)
        {
            string fullPath = Path.Combine(outputDir, entry.FullName);
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(fullPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using var entryStream = entry.Open();
            using var fileStream = File.Create(fullPath);
            entryStream.CopyTo(fileStream);
        }
    }
}