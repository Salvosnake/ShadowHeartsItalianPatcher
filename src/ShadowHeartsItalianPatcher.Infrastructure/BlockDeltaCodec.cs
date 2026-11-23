using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using ShadowHeartsItalianPatcher.Core;
namespace ShadowHeartsItalianPatcher.Infrastructure;
// SHID v1: exact COPY/LITERAL/ZERO instructions in a Brotli stream. No executable content.
public sealed class BlockDeltaCodec : IDeltaCodec
{
    public const int Block = 2048, MaxLiteral = 4 * 1024 * 1024;
    static readonly byte[] Magic = "SHID0001"u8.ToArray();
    static UInt128 Key(ReadOnlySpan<byte> b)
    {
        Span<byte> h = stackalloc byte[32];
        SHA256.HashData(b, h);
        return ((UInt128)BinaryPrimitives.ReadUInt64LittleEndian(h) << 64) | BinaryPrimitives.ReadUInt64LittleEndian(h[8..]);
    }
    static async Task Number(Stream s, long n, CancellationToken ct)
    {
        byte[] b = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(b, n);
        await s.WriteAsync(b, ct);
    }
    static async Task<long> Number(Stream s, CancellationToken ct)
    {
        byte[] b = new byte[8];
        await s.ReadExactlyAsync(b, ct);
        return BinaryPrimitives.ReadInt64LittleEndian(b);
    }
    public async Task EncodeAsync(Stream source, Stream target, Stream patch, IProgress<PatchProgress>? progress, CancellationToken ct)
    {
        Files.Require(source.CanSeek && target.CanSeek, "I file sorgenti devono supportare offset a64bit.");
        long blocks = (source.Length + Block - 1) / Block;
        Files.Require(blocks <= 4_000_000, "Generatore: sorgente troppo grande per l'indice da2048byte (limite8GB). Il decoder non ha questo limite.");
        var index = new Dictionary<UInt128, long>((int)blocks);
        byte[] chunk = new byte[1024 * 1024];
        source.Position = 0;
        long at = 0;
        while (at < source.Length)
        {
            int size = (int)Math.Min(chunk.Length, source.Length - at);
            await source.ReadExactlyAsync(chunk.AsMemory(0, size), ct);
            for (int p = 0; p + Block <= size; p += Block)
                index.TryAdd(Key(chunk.AsSpan(p, Block)), at + p);
            at += size;
            progress?.Report(new("Indicizzazione sorgente", 20.0 * at / source.Length));
        }
        await patch.WriteAsync(Magic, ct);
        await Number(patch, target.Length, ct);
        await using var body = new BrotliStream(patch, CompressionLevel.Optimal, true);
        byte[] buf = new byte[Block], check = new byte[Block];
        byte[] literal = new byte[MaxLiteral];
        int lit = 0;
        long copied = 0, copyAt = 0, zeros = 0;
        target.Position = 0;
        long done = 0, last = -1;
        async Task FlushCopy()
        {
            if (copied > 0)
            {
                await body.WriteAsync(new byte[] { 1 }, ct);
                await Number(body, copyAt, ct);
                await Number(body, copied, ct);
                copied = 0;
            }
        }
        async Task FlushZero()
        {
            if (zeros > 0)
            {
                await body.WriteAsync(new byte[] { 3 }, ct);
                await Number(body, zeros, ct);
                zeros = 0;
            }
        }
        async Task FlushLiteral()
        {
            if (lit > 0)
            {
                await body.WriteAsync(new byte[] { 2 }, ct);
                await Number(body, lit, ct);
                await body.WriteAsync(literal.AsMemory(0, lit), ct);
                lit = 0;
            }
        }
        while (done < target.Length)
        {
            ct.ThrowIfCancellationRequested();
            int n = (int)Math.Min(Block, target.Length - done);
            await target.ReadExactlyAsync(buf.AsMemory(0, n), ct);
            if (buf.AsSpan(0, n).IndexOfAnyExcept((byte)0) < 0)
            {
                await FlushCopy();
                await FlushLiteral();
                zeros += n;
            }
            else
            {
                await FlushZero();
                long offset = 0;
                bool match = n == Block && index.TryGetValue(Key(buf), out offset);
                if (match)
                {
                    source.Position = offset;
                    await source.ReadExactlyAsync(check, ct);
                    match = buf.AsSpan().SequenceEqual(check);
                }
                if (match)
                {
                    await FlushLiteral();
                    if (copied > 0 && copyAt + copied != offset)
                        await FlushCopy();
                    if (copied == 0)
                        copyAt = offset;
                    copied += n;
                }
                else
                {
                    await FlushCopy();
                    if (lit + n > literal.Length)
                        await FlushLiteral();
                    buf.AsSpan(0, n).CopyTo(literal.AsSpan(lit));
                    lit += n;
                }
            }
            done += n;
            if (done - last >= 16 * 1024 * 1024)
            {
                progress?.Report(new("Generazione patch differenziale", 20 + 80.0 * done / target.Length));
                last = done;
            }
        }
        await FlushCopy();
        await FlushZero();
        await FlushLiteral();
        await body.WriteAsync(new byte[] { 0 }, ct);
    }
    public async Task ApplyAsync(Stream source, Stream patch, Stream target, long expectedSize, IProgress<PatchProgress>? progress, CancellationToken ct)
    {
        byte[] magic = new byte[8];
        await patch.ReadExactlyAsync(magic, ct);
        Files.Require(magic.AsSpan().SequenceEqual(Magic), "Patch non riconosciuta o corrotta.");
        Files.Require(await Number(patch, ct) == expectedSize, "Dimensione della patch incompatibile col manifest.");
        await using var body = new BrotliStream(patch, CompressionMode.Decompress, true);
        byte[] buffer = new byte[1024 * 1024];
        byte[] op = new byte[1];
        long written = 0, last = -1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await body.ReadExactlyAsync(op, ct);
            if (op[0] == 0)
            {
                Files.Require(written == expectedSize, "Patch troncata: dimensione finale errata.");
                Files.Require(await body.ReadAsync(op, ct) == 0, "Dati inattesi dopo la fine della patch.");
                break;
            }
            Files.Require(op[0] is 1 or 2 or 3, "Istruzione della patch non valida.");
            long offset = op[0] == 1 ? await Number(body, ct) : 0;
            long size = await Number(body, ct);
            Files.Require(size > 0 && size <= expectedSize - written, "Patch corrotta: istruzione fuori dai limiti della ISO.");
            if (op[0] == 1)
            {
                Files.Require(offset >= 0 && size <= source.Length && offset <= source.Length - size, "Patch corrotta: lettura fuori dai limiti della sorgente.");
                source.Position = offset;
            }
            if (op[0] == 2)
                Files.Require(size <= MaxLiteral, "Blocco letterale della patch troppo grande.");
            if (op[0] == 3)
                Array.Clear(buffer);
            long left = size;
            while (left > 0)
            {
                int n = (int)Math.Min(buffer.Length, left);
                if (op[0] != 3)
                    await (op[0] == 1 ? source : body).ReadExactlyAsync(buffer.AsMemory(0, n), ct);
                await target.WriteAsync(buffer.AsMemory(0, n), ct);
                left -= n;
                written += n;
                ct.ThrowIfCancellationRequested();
                if (written - last >= 16 * 1024 * 1024)
                {
                    progress?.Report(new("Scrittura ISO italiana", 35 + 40.0 * written / expectedSize));
                    last = written;
                }
            }
        }
    }
}
