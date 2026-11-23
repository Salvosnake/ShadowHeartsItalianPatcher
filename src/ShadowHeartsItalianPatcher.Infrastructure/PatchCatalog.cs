using System.Text.Json;
using System.Text.RegularExpressions;
using ShadowHeartsItalianPatcher.Core;
namespace ShadowHeartsItalianPatcher.Infrastructure;

public sealed class PatchCatalog : IPatchCatalog
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public static void Validate(PatchManifest m) => ManifestRules.Validate(m);
    public async Task<IReadOnlyList<PatchPackage>> LoadAsync(string directory, CancellationToken ct)
    {
        if (!System.IO.Directory.Exists(directory))
            return [];
        var list = new List<PatchPackage>();
        foreach (var file in System.IO.Directory.EnumerateFiles(directory, "manifest.json", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            ct.ThrowIfCancellationRequested();
            Files.Require(new FileInfo(file).Length <= 65536, "Manifest troppo grande.");
            await using var s = Files.Read(file);
            PatchManifest m;
            try
            {
                m = (await JsonSerializer.DeserializeAsync<PatchManifest>(s, Json, ct)) ?? throw new JsonException();
            }
            catch (JsonException) { throw new PatcherException($"Manifest non valido: {file}"); }
            Validate(m);
            list.Add(new(m, Path.GetDirectoryName(Path.GetFullPath(file))!));
        }
        return list;
    }
}
