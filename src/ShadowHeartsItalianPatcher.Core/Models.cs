namespace ShadowHeartsItalianPatcher.Core;

public static class PatcherVersion
{
    public const string Current = "1.0.0";
}
public sealed record IsoIdentity(string DiscId, int DiscNumber, string Region, long Size, string Sha256 = "");
public sealed record PatchProgress(string Stage, double Percent);
public sealed class PatcherException(string message) : Exception(message);
public sealed record PatchManifest
{
    public int SchemaVersion { get; init; } = 1;
    public string Algorithm { get; init; } = "shid-brotli-v1";
    public string TranslationVersion { get; init; } = "";
    public string DiscId { get; init; } = "";
    public string SourceSha256 { get; init; } = "";
    public long SourceSize
    {
        get; init;
    }
    public string PatchFile { get; init; } = "translation.shid";
    public string PatchSha256 { get; init; } = "";
    public long PatchSize
    {
        get; init;
    }
    public string TargetSha256 { get; init; } = "";
    public long TargetSize
    {
        get; init;
    }
    public string MinimumPatcherVersion { get; init; } = "1.0.0";
    public string Compatibility { get; init; } = "Candidata beta: collaudo completo non concluso.";
    public string DistributionReview { get; init; } = "Richiesta: contiene differenze binarie derivate dal gioco. Non pubblicata.";
}
public sealed record PatchPackage(PatchManifest Manifest, string Directory);
public sealed record Inspection(IsoIdentity Iso, PatchPackage? Package);
public interface IIsoInspector
{
    Task<IsoIdentity> InspectAsync(Stream stream, CancellationToken ct);
}
public interface IPatchCatalog
{
    Task<IReadOnlyList<PatchPackage>> LoadAsync(string directory, CancellationToken ct);
}
public interface IStorageSpace
{
    long AvailableBytes(string directory);
}
public interface IPatchService
{
    Task<Inspection> InspectAsync(string source, string patches, IProgress<PatchProgress>? progress, CancellationToken ct);
    Task<string> ApplyAsync(string source, string destinationDirectory, PatchPackage package, IProgress<PatchProgress>? progress, CancellationToken ct);
}
public interface IDeltaCodec
{
    Task EncodeAsync(Stream source, Stream target, Stream patch, IProgress<PatchProgress>? progress, CancellationToken ct);
    Task ApplyAsync(Stream source, Stream patch, Stream target, long expectedSize, IProgress<PatchProgress>? progress, CancellationToken ct);
}
