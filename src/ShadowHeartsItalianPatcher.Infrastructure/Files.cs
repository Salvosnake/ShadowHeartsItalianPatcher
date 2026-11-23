using System.Security.Cryptography;
using ShadowHeartsItalianPatcher.Core;
namespace ShadowHeartsItalianPatcher.Infrastructure;

public static class Files
{
    public static FileStream Read(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
    public static FileStream Create(string path) => new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.Asynchronous);
    public static async Task<string> HashAsync(Stream stream, IProgress<PatchProgress>? progress, CancellationToken ct, string stage = "Verifica SHA-256", double start = 0, double span = 100)
    {
        stream.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[1024 * 1024];
        long n = 0;
        int read;
        long last = -1;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            hash.AppendData(buffer, 0, read);
            n += read;
            if (n - last >= 16 * 1024 * 1024)
            {
                progress?.Report(new(stage, start + span * n / Math.Max(1, stream.Length)));
                last = n;
            }
        }
        progress?.Report(new(stage, start + span));
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    public static void Require(bool ok, string message)
    {
        if (!ok)
            throw new PatcherException(message);
    }
    public static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
public sealed class StorageSpace : IStorageSpace
{
    public long AvailableBytes(string directory)
    {
        var path = Path.GetFullPath(directory);
        var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var drive = DriveInfo.GetDrives().Where(d => d.IsReady && (path.Equals(d.Name, cmp) || path.StartsWith(Path.TrimEndingDirectorySeparator(d.Name) + Path.DirectorySeparatorChar, cmp))).OrderByDescending(d => d.Name.Length).FirstOrDefault();
        return (drive ?? new DriveInfo(Path.GetPathRoot(path)!)).AvailableFreeSpace;
    }
}
