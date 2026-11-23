using System.Text.RegularExpressions;
namespace ShadowHeartsItalianPatcher.Core;

public static class ManifestRules
{
    static void Require(bool ok, string message)
    {
        if (!ok)
            throw new PatcherException(message);
    }
    public static void Validate(PatchManifest m)
    {
        Require(m.SchemaVersion == 1 && m.Algorithm == "shid-brotli-v1", "Formato del pacchetto non supportato.");
        Require(m.DiscId is "SLUS_210.41" or "SLUS_210.44", "Disco del pacchetto non supportato.");
        Require(Version.TryParse(m.MinimumPatcherVersion, out var min) && min <= Version.Parse(PatcherVersion.Current), "Questo pacchetto richiede una versione più recente del patcher.");
        Require(Version.TryParse(m.TranslationVersion, out _), "Versione della traduzione non valida.");
        foreach (var hash in new[] { m.SourceSha256, m.TargetSha256, m.PatchSha256 })
            Require(hash is not null && Regex.IsMatch(hash, "^[0-9a-f]{64}$"), "Hash mancante o non valido nel manifest.");
        Require(m.SourceSize > 0 && m.TargetSize > 0 && m.PatchSize > 0, "Dimensioni non valide nel manifest.");
        Require(!string.IsNullOrWhiteSpace(m.PatchFile) && m.PatchFile == Path.GetFileName(m.PatchFile) && m.PatchFile.IndexOfAny(['/', '\\', ':']) < 0 && m.PatchFile is not "." and not "..", "Percorso della patch non sicuro.");
    }
    public static string OutputName(string discId) => discId switch { "SLUS_210.41" => "ShadowHeartsCovenant_USA_Disc1_Italian.iso", "SLUS_210.44" => "ShadowHeartsCovenant_USA_Disc2_Italian.iso", _ => throw new PatcherException("Disco non supportato.") };
}
