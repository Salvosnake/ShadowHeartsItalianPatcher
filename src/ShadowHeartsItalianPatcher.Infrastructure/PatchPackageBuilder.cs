using System.Diagnostics;
using System.Text.Json;
using ShadowHeartsItalianPatcher.Core;
namespace ShadowHeartsItalianPatcher.Infrastructure;

public sealed class PatchPackageBuilder(IIsoInspector inspector, IDeltaCodec codec)
{
    public async Task<PatchPackage> BuildAsync(string source, string target, string directory, string translationVersion, string compatibility, IProgress<PatchProgress>? progress, CancellationToken ct)
    {
        Files.Require(!Files.SamePath(source, target), "Sorgente e destinazione di riferimento devono essere distinte.");
        Files.Require(Version.TryParse(translationVersion, out _), "Usa una versione numerica, ad esempio2026.11.18.");
        Files.Require(!Directory.Exists(directory), "La cartella del nuovo pacchetto deve essere inesistente.");
        await using var input = Files.Read(source);
        await using var output = Files.Read(target);
        var a = await inspector.InspectAsync(input, ct);
        var b = await inspector.InspectAsync(output, ct);
        Files.Require(a.DiscId == b.DiscId, "Le due ISO appartengono a dischi differenti.");
        string sourceHash = await Files.HashAsync(input, progress, ct, "Hash ISO originale");
        string targetHash = await Files.HashAsync(output, progress, ct, "Hash ISO italiana di riferimento");
        ct.ThrowIfCancellationRequested();
        string finalDirectory = Path.GetFullPath(directory);
        string stage = finalDirectory + ".building-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stage);
        string patchPath = Path.Combine(stage, "translation.shid");
        bool committed = false;
        var timer = Stopwatch.StartNew();
        try
        {
            await using (var patch = Files.Create(patchPath))
            {
                await codec.EncodeAsync(input, output, patch, progress, ct);
                await patch.FlushAsync(ct);
                patch.Flush(true);
            }
            string patchHash;
            long patchSize;
            await using (var patch = Files.Read(patchPath))
            {
                patchSize = patch.Length;
                patchHash = await Files.HashAsync(patch, null, ct);
            }
            Files.Require(await Files.HashAsync(input, null, ct) == sourceHash && await Files.HashAsync(output, null, ct) == targetHash, "Una ISO di riferimento è cambiata durante la generazione.");
            var manifest = new PatchManifest { TranslationVersion = translationVersion, DiscId = a.DiscId, SourceSha256 = sourceHash, SourceSize = input.Length, TargetSha256 = targetHash, TargetSize = output.Length, PatchSha256 = patchHash, PatchSize = patchSize, Compatibility = compatibility };
            PatchCatalog.Validate(manifest);
            await using (var file = Files.Create(Path.Combine(stage, "manifest.json")))
                await JsonSerializer.SerializeAsync(file, manifest, PatchCatalog.Json, ct);
            await File.WriteAllTextAsync(Path.Combine(stage, "generation-report.json"), JsonSerializer.Serialize(new
            {
                algorithm = manifest.Algorithm,
                seconds = timer.Elapsed.TotalSeconds,
                sourceBytes = input.Length,
                targetBytes = output.Length,
                patchBytes = patchSize,
                processPeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64,
                distributionReview = manifest.DistributionReview
            }, PatchCatalog.Json), ct);
            Directory.Move(stage, finalDirectory);
            committed = true;
            return new(manifest, finalDirectory);
        }
        finally { if (!committed) { foreach (var name in new[] { "translation.shid", "manifest.json", "generation-report.json" }) { var file = Path.Combine(stage, name); if (File.Exists(file)) File.Delete(file); } if (!Directory.EnumerateFileSystemEntries(stage).Any()) Directory.Delete(stage); } }
    }
}
