using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ShadowHeartsItalianPatcher.Core;
using ShadowHeartsItalianPatcher.Infrastructure;
namespace ShadowHeartsItalianPatcher.Tests;

public sealed class PatcherTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "ship-test-" + Guid.NewGuid().ToString("N"));
    public PatcherTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    string PathFor(string name) => Path.Combine(root, name);
    static void Both(byte[] b, int p, uint n)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(p), n);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(p + 4), n);
    }
    static byte[] Record(string name, uint lba, uint size)
    {
        byte[] text = Encoding.ASCII.GetBytes(name);
        byte[] b = new byte[33 + text.Length + (text.Length % 2 == 0 ? 1 : 0)];
        b[0] = (byte)b.Length;
        Both(b, 2, lba);
        Both(b, 10, size);
        b[32] = (byte)text.Length;
        text.CopyTo(b, 33);
        return b;
    }
    public static byte[] Iso(string id = "SLUS_210.41")
    {
        byte[] b = new byte[200 * 2048];
        new Random(33).NextBytes(b);
        Array.Clear(b, 16 * 2048, 2048);
        Array.Clear(b, 20 * 2048, 2048);
        int p = 16 * 2048;
        b[p] = 1;
        "CD001"u8.CopyTo(b.AsSpan(p + 1));
        b[p + 6] = 1;
        Both(b, p + 80, 200);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(p + 128), 2048);
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(p + 130), 2048);
        var rec = Record("\0", 20, 2048);
        rec.CopyTo(b, p + 156);
        string cnf = $"BOOT2 = cdrom0:\\{id};1\r\nVER = 1.00\r\nVMODE = NTSC\r\n";
        byte[] text = Encoding.ASCII.GetBytes(cnf);
        text.CopyTo(b, 21 * 2048);
        var a = Record("SYSTEM.CNF;1", 21, (uint)text.Length);
        a.CopyTo(b, 20 * 2048);
        var e = Record(id + ";1", 22, 64);
        e.CopyTo(b, 20 * 2048 + a.Length);
        new byte[] { 0x7f, 69, 76, 70 }.CopyTo(b, 22 * 2048);
        return b;
    }
    async Task<(string Source, string Target, PatchPackage Package)> Package(string id = "SLUS_210.41")
    {
        string source = PathFor("original.iso"), target = PathFor("reference.iso");
        var a = Iso(id);
        var b = (byte[])a.Clone();
        Array.Clear(b, 35 * 2048, 4096);
        b[58 * 2048] = 44;
        await File.WriteAllBytesAsync(source, a);
        await File.WriteAllBytesAsync(target, b);
        var package = await new PatchPackageBuilder(new IsoInspector(), new BlockDeltaCodec()).BuildAsync(source, target, PathFor("Patches/disc"), "2026.11.18", "Beta sintetica", null, default);
        return (source, target, package);
    }
    PatchService Service(IStorageSpace? space = null, IDeltaCodec? codec = null) => new(new IsoInspector(), new PatchCatalog(), codec ?? new BlockDeltaCodec(), space ?? new FakeSpace(long.MaxValue));
    [Theory]
    [InlineData("SLUS_210.41", 1)]
    [InlineData("SLUS_210.44", 2)]
    public async Task DetectBothDiscs(string id, int number)
    {
        using var s = new MemoryStream(Iso(id));
        var result = await new IsoInspector().InspectAsync(s, default);
        Assert.Equal(number, result.DiscNumber);
        Assert.Equal("USA / NTSC", result.Region);
    }
    [Fact]
    public async Task RejectCorrupt()
    {
        var b = Iso();
        b[16 * 2048 + 1] = 0;
        await Assert.ThrowsAsync<PatcherException>(() => new IsoInspector().InspectAsync(new MemoryStream(b), default));
    }
    [Fact]
    public async Task RejectPal()
    {
        await Assert.ThrowsAsync<PatcherException>(() => new IsoInspector().InspectAsync(new MemoryStream(Iso("SLES_820.30")), default));
    }
    [Fact]
    public async Task RejectOutOfBounds()
    {
        var b = Iso();
        Both(b, 16 * 2048 + 158, uint.MaxValue);
        await Assert.ThrowsAsync<PatcherException>(() => new IsoInspector().InspectAsync(new MemoryStream(b), default));
    }
    [Fact]
    public async Task RejectTruncated()
    {
        var b = Iso()[..^2048];
        await Assert.ThrowsAsync<PatcherException>(() => new IsoInspector().InspectAsync(new MemoryStream(b), default));
    }
    [Theory]
    [InlineData("SLUS_210.41")]
    [InlineData("SLUS_210.44")]
    public async Task RoundTripExactAndOriginalPreserved(string id)
    {
        var p = await Package(id);
        var before = await File.ReadAllBytesAsync(p.Source);
        var inspected = await Service().InspectAsync(p.Source, PathFor("Patches"), null, default);
        Assert.NotNull(inspected.Package);
        var dest = Directory.CreateDirectory(PathFor("output")).FullName;
        var output = await Service().ApplyAsync(p.Source, dest, p.Package, null, default);
        Assert.Equal(await File.ReadAllBytesAsync(p.Target), await File.ReadAllBytesAsync(output));
        Assert.Equal(before, await File.ReadAllBytesAsync(p.Source));
        Assert.Empty(Directory.GetFiles(dest, "*.partial"));
    }
    [Fact]
    public async Task ModifiedSourceRejected()
    {
        var p = await Package();
        var b = await File.ReadAllBytesAsync(p.Source);
        b[40 * 2048] ^= 1;
        await File.WriteAllBytesAsync(p.Source, b);
        var r = await Service().InspectAsync(p.Source, PathFor("Patches"), null, default);
        Assert.Null(r.Package);
        Directory.CreateDirectory(PathFor("out"));
        await Assert.ThrowsAsync<PatcherException>(() => Service().ApplyAsync(p.Source, PathFor("out"), p.Package, null, default));
    }
    [Fact]
    public async Task InsufficientSpace()
    {
        var p = await Package();
        Directory.CreateDirectory(PathFor("out"));
        await Assert.ThrowsAsync<PatcherException>(() => Service(new FakeSpace(0)).ApplyAsync(p.Source, PathFor("out"), p.Package, null, default));
        Assert.Empty(Directory.GetFiles(PathFor("out")));
    }
    [Fact]
    public async Task CorruptPatchHash()
    {
        var p = await Package();
        var path = Path.Combine(p.Package.Directory, p.Package.Manifest.PatchFile);
        var b = await File.ReadAllBytesAsync(path);
        b[^1] ^= 1;
        await File.WriteAllBytesAsync(path, b);
        Directory.CreateDirectory(PathFor("out"));
        await Assert.ThrowsAsync<PatcherException>(() => Service().ApplyAsync(p.Source, PathFor("out"), p.Package, null, default));
    }
    [Fact]
    public async Task BadTargetHashCleansTemporary()
    {
        var p = await Package();
        Directory.CreateDirectory(PathFor("out"));
        var bad = p.Package with
        {
            Manifest = p.Package.Manifest with
            {
                TargetSha256 = new string('0', 64)
            }
        };
        await Assert.ThrowsAsync<PatcherException>(() => Service().ApplyAsync(p.Source, PathFor("out"), bad, null, default));
        Assert.Empty(Directory.GetFiles(PathFor("out")));
    }
    [Fact]
    public async Task WrongDiscRejected()
    {
        var p = await Package();
        Directory.CreateDirectory(PathFor("out"));
        var bad = p.Package with
        {
            Manifest = p.Package.Manifest with
            {
                DiscId = "SLUS_210.44"
            }
        };
        await Assert.ThrowsAsync<PatcherException>(() => Service().ApplyAsync(p.Source, PathFor("out"), bad, null, default));
    }
    [Theory]
    [InlineData("../other.shid")]
    [InlineData("/tmp/other.shid")]
    [InlineData("C:\\other.shid")]
    public async Task RejectPathTraversal(string path)
    {
        var p = await Package();
        Assert.Throws<PatcherException>(() => PatchCatalog.Validate(p.Package.Manifest with { PatchFile = path }));
    }
    [Fact]
    public async Task FutureVersionRejected()
    {
        var p = await Package();
        Assert.Throws<PatcherException>(() => PatchCatalog.Validate(p.Package.Manifest with { MinimumPatcherVersion = "99.0.0" }));
    }
    [Fact]
    public async Task CancellationMidWriteCleansOnlyOwnFile()
    {
        var p = await Package();
        using var cts = new CancellationTokenSource();
        var dir = Directory.CreateDirectory(PathFor("out")).FullName;
        await File.WriteAllTextAsync(Path.Combine(dir, "keep.txt"), "keep");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(codec: new FailureCodec(() => { cts.Cancel(); cts.Token.ThrowIfCancellationRequested(); })).ApplyAsync(p.Source, dir, p.Package, null, cts.Token));
        Assert.Equal(new[] { "keep.txt" }, Directory.GetFiles(dir).Select(Path.GetFileName));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(dir, "keep.txt")));
    }
    [Fact]
    public async Task WriteFailureCleansTemporary()
    {
        var p = await Package();
        Directory.CreateDirectory(PathFor("out"));
        await Assert.ThrowsAsync<IOException>(() => Service(codec: new FailureCodec(() => throw new IOException("Simulated interruption"))).ApplyAsync(p.Source, PathFor("out"), p.Package, null, default));
        Assert.Empty(Directory.GetFiles(PathFor("out")));
    }
    [Fact]
    public async Task ExistingOutputNeverOverwritten()
    {
        var p = await Package();
        var dir = Directory.CreateDirectory(PathFor("out")).FullName;
        var output = Path.Combine(dir, "ShadowHeartsCovenant_USA_Disc1_Italian.iso");
        await File.WriteAllTextAsync(output, "keep");
        await Assert.ThrowsAsync<PatcherException>(() => Service().ApplyAsync(p.Source, dir, p.Package, null, default));
        Assert.Equal("keep", await File.ReadAllTextAsync(output));
    }
    static MemoryStream Encoded(long size, Action<BinaryWriter> instructions)
    {
        var stream = new MemoryStream();
        stream.Write("SHID0001"u8);
        using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
            w.Write(size);
        using (var compressed = new BrotliStream(stream, CompressionLevel.Fastest, true))
        using (var w = new BinaryWriter(compressed))
        {
            instructions(w);
        }
        stream.Position = 0;
        return stream;
    }
    [Fact]
    public async Task SupportsOffsetsBeyondFourGiB()
    {
        var path = PathFor("sparse.bin");
        await using var source = Files.Create(path);
        long at = (1L << 32) + 2048;
        source.SetLength(at + 4);
        source.Position = at;
        await source.WriteAsync(new byte[] { 1, 2, 3, 4 });
        source.Flush(true);
        using var patch = Encoded(4, w => { w.Write((byte)1); w.Write(at); w.Write(4L); w.Write((byte)0); });
        using var target = new MemoryStream();
        await new BlockDeltaCodec().ApplyAsync(source, patch, target, 4, null, default);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, target.ToArray());
    }
    [Fact]
    public async Task SupportsOutputBeyondFourGiBWithoutAllocatingIt()
    {
        long size = (1L << 32) + 2048;
        using var patch = Encoded(size, w => { w.Write((byte)3); w.Write(size); w.Write((byte)0); });
        using var target = new CountingSink();
        await new BlockDeltaCodec().ApplyAsync(new MemoryStream(), patch, target, size, null, default);
        Assert.Equal(size, target.Bytes);
    }
    [Fact]
    public async Task RejectOverflowCopy()
    {
        using var patch = Encoded(4, w => { w.Write((byte)1); w.Write(long.MaxValue); w.Write(4L); w.Write((byte)0); });
        await Assert.ThrowsAsync<PatcherException>(() => new BlockDeltaCodec().ApplyAsync(new MemoryStream(new byte[8]), patch, new MemoryStream(), 4, null, default));
    }
    [Fact]
    public async Task RejectOversizedLiteral()
    {
        using var patch = Encoded(9_000_000, w => { w.Write((byte)2); w.Write(9_000_000L); });
        await Assert.ThrowsAsync<PatcherException>(() => new BlockDeltaCodec().ApplyAsync(new MemoryStream(), patch, new MemoryStream(), 9_000_000, null, default));
    }
    [Fact]
    public async Task RejectTruncatedInstructions()
    {
        using var patch = Encoded(4, w => w.Write((byte)1));
        await Assert.ThrowsAnyAsync<IOException>(() => new BlockDeltaCodec().ApplyAsync(new MemoryStream(), patch, new MemoryStream(), 4, null, default));
    }
    [Fact]
    public async Task RejectEarlyEnd()
    {
        using var patch = Encoded(4, w => w.Write((byte)0));
        await Assert.ThrowsAsync<PatcherException>(() => new BlockDeltaCodec().ApplyAsync(new MemoryStream(), patch, new MemoryStream(), 4, null, default));
    }
    [Fact]
    public async Task ReorderedBlocksRemainExact()
    {
        byte[] a = new byte[8192];
        new Random(4).NextBytes(a);
        byte[] b = a[4096..].Concat(a[..4096]).ToArray();
        using var patch = new MemoryStream();
        await new BlockDeltaCodec().EncodeAsync(new MemoryStream(a), new MemoryStream(b), patch, null, default);
        patch.Position = 0;
        using var result = new MemoryStream();
        await new BlockDeltaCodec().ApplyAsync(new MemoryStream(a), patch, result, b.Length, null, default);
        Assert.Equal(b, result.ToArray());
    }
    [Fact]
    public async Task PackageFailureRemovesOnlyOwnedStaging()
    {
        var a = PathFor("original.iso");
        var b = PathFor("reference.iso");
        await File.WriteAllBytesAsync(a, Iso());
        await File.WriteAllBytesAsync(b, Iso());
        var output = PathFor("new-package");
        await Assert.ThrowsAsync<NotSupportedException>(() => new PatchPackageBuilder(new IsoInspector(), new FailureCodec(() => { })).BuildAsync(a, b, output, "1.0.0", "test", null, default));
        Assert.False(Directory.Exists(output));
        Assert.Empty(Directory.GetDirectories(root, "*.building-*"));
        Assert.True(File.Exists(a) && File.Exists(b));
    }
    [Fact]
    public async Task ContentAuditAccountsForEveryByte()
    {
        var p = await Package();
        var text = JsonSerializer.Serialize(await PatchAudit.InspectAsync(p.Package, default));
        using var doc = JsonDocument.Parse(text);
        var r = doc.RootElement;
        Assert.Equal(p.Package.Manifest.TargetSize, r.GetProperty("copiedBytes").GetInt64() + r.GetProperty("literalBytes").GetInt64() + r.GetProperty("zeroBytes").GetInt64());
        Assert.False(r.GetProperty("publicDistributionApproved").GetBoolean());
    }
    sealed class FakeSpace(long bytes) : IStorageSpace
    {
        public long AvailableBytes(string path) => bytes;
    }
    sealed class FailureCodec(Action fail) : IDeltaCodec
    {
        public Task EncodeAsync(Stream a, Stream b, Stream c, IProgress<PatchProgress>? p, CancellationToken ct) => throw new NotSupportedException(); public async Task ApplyAsync(Stream a, Stream b, Stream c, long n, IProgress<PatchProgress>? p, CancellationToken ct)
        {
            await c.WriteAsync(new byte[16], ct);
            fail();
        }
    }
    sealed class CountingSink : Stream
    {
        public long Bytes; public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true; public override long Length => Bytes; public override long Position
        {
            get => Bytes; set => throw new NotSupportedException();
        }
        public override void Flush()
        {
        }
        public override int Read(byte[] b, int o, int c) => throw new NotSupportedException(); public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException(); public override void SetLength(long n) => throw new NotSupportedException(); public override void Write(byte[] b, int o, int c) => Bytes += c; public override ValueTask WriteAsync(ReadOnlyMemory<byte> b, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Bytes += b.Length;
            return ValueTask.CompletedTask;
        }
    }
}
