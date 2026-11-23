using ShadowHeartsItalianPatcher.Core;
namespace ShadowHeartsItalianPatcher.Infrastructure;

public sealed class PatchService(IIsoInspector inspector, IPatchCatalog catalog, IDeltaCodec codec, IStorageSpace space) : IPatchService
{
    public async Task<Inspection> InspectAsync(string source, string patches, IProgress<PatchProgress>? progress, CancellationToken ct)
    {
        await using var stream = Files.Read(source);
        var iso = await inspector.InspectAsync(stream, ct);
        var packages = await catalog.LoadAsync(patches, ct);
        string hash = await Files.HashAsync(stream, progress, ct);
        iso = iso with
        {
            Sha256 = hash
        };
        var package = packages.Where(p => p.Manifest.DiscId == iso.DiscId && p.Manifest.SourceSize == iso.Size && p.Manifest.SourceSha256 == hash).OrderByDescending(p => Version.Parse(p.Manifest.TranslationVersion)).FirstOrDefault();
        return new(iso, package);
    }
    public async Task<string> ApplyAsync(string source, string destinationDirectory, PatchPackage package, IProgress<PatchProgress>? progress, CancellationToken ct)
    {
        var m = package.Manifest;
        PatchCatalog.Validate(m);
        Files.Require(Directory.Exists(destinationDirectory), "La cartella di destinazione non esiste.");
        string name = ManifestRules.OutputName(m.DiscId);
        string destination = Path.Combine(Path.GetFullPath(destinationDirectory), name);
        Files.Require(!Files.SamePath(source, destination), "La destinazione coincide con la ISO originale.");
        Files.Require(!File.Exists(destination), "La ISO di destinazione esiste già. Scegli un'altra cartella: non verrà sovrascritta.");
        Files.Require(m.TargetSize <= long.MaxValue - 16 * 1024 * 1024 && space.AvailableBytes(destinationDirectory) >= m.TargetSize + 16 * 1024 * 1024, "Spazio su disco insufficiente per la nuova ISO.");
        string patchPath = Path.Combine(package.Directory, m.PatchFile);
        await using var patch = Files.Read(patchPath);
        Files.Require(patch.Length == m.PatchSize, "Dimensione della patch errata.");
        Files.Require(await Files.HashAsync(patch, progress, ct, "Verifica pacchetto", 0, 5) == m.PatchSha256, "Patch corrotta: SHA-256 incompatibile.");
        await using var input = Files.Read(source);
        var identity = await inspector.InspectAsync(input, ct);
        Files.Require(identity.DiscId == m.DiscId && input.Length == m.SourceSize, "ISO sorgente incompatibile con questa patch.");
        Files.Require(await Files.HashAsync(input, progress, ct, "Verifica ISO originale", 5, 30) == m.SourceSha256, "SHA-256 della ISO non supportato: immagine differente, modificata o corrotta. Nessuna patch applicata.");
        ct.ThrowIfCancellationRequested();
        string temp = Path.Combine(destinationDirectory, ".ship-" + Guid.NewGuid().ToString("N") + ".partial");
        bool owned = false;
        try
        {
            await using (var output = Files.Create(temp))
            {
                owned = true;
                patch.Position = 0;
                await codec.ApplyAsync(input, patch, output, m.TargetSize, progress, ct);
                await output.FlushAsync(ct);
                output.Flush(true);
                Files.Require(output.Length == m.TargetSize, "La ISO generata ha dimensione errata.");
                Files.Require(await Files.HashAsync(output, progress, ct, "Verifica ISO italiana", 75, 15) == m.TargetSha256, "SHA-256 finale errato: la ISO temporanea verrà eliminata.");
                var target = await inspector.InspectAsync(output, ct);
                Files.Require(target.DiscId == m.DiscId, "Identificatore della ISO generata non valido.");
            }
            // Reopen the original pathname too, so replacement of the path during the job is detected.
            await using (var verify = Files.Read(source))
            {
                Files.Require(verify.Length == m.SourceSize && await Files.HashAsync(verify, progress, ct, "Verifica conservazione originale", 90, 9) == m.SourceSha256, "La ISO sorgente è cambiata durante l'operazione.");
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temp, destination, false);
            owned = false;
            progress?.Report(new("ISO italiana verificata e salvata", 100));
            return destination;
        }
        finally { if (owned && File.Exists(temp)) File.Delete(temp); }
    }
}
