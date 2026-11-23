using System.Buffers.Binary;
using System.IO.Compression;
using ShadowHeartsItalianPatcher.Core;
namespace ShadowHeartsItalianPatcher.Infrastructure;

public static class PatchAudit
{
    static async Task<long> Number(Stream stream, CancellationToken ct)
    {
        byte[] b = new byte[8];
        await stream.ReadExactlyAsync(b, ct);
        return BinaryPrimitives.ReadInt64LittleEndian(b);
    }
    public static async Task<object> InspectAsync(PatchPackage package, CancellationToken ct)
    {
        var m = package.Manifest;
        PatchCatalog.Validate(m);
        await using var file = Files.Read(Path.Combine(package.Directory, m.PatchFile));
        Files.Require(file.Length == m.PatchSize && await Files.HashAsync(file, null, ct) == m.PatchSha256, "Hash del pacchetto errato.");
        file.Position = 0;
        byte[] head = new byte[8];
        await file.ReadExactlyAsync(head, ct);
        Files.Require(head.AsSpan().SequenceEqual("SHID0001"u8) && await Number(file, ct) == m.TargetSize, "Header patch errato.");
        await using var body = new BrotliStream(file, CompressionMode.Decompress);
        byte[] op = new byte[1], buffer = new byte[1024 * 1024];
        long produced = 0, copied = 0, literal = 0, zero = 0, operations = 0, elf = 0, iso = 0, tim2 = 0;
        while (true)
        {
            await body.ReadExactlyAsync(op, ct);
            if (op[0] == 0)
            {
                Files.Require(produced == m.TargetSize && await body.ReadAsync(op, ct) == 0, "Dimensione finale o fine patch errata.");
                break;
            }
            Files.Require(op[0] is 1 or 2 or 3, "Opcode patch errato.");
            long offset = op[0] == 1 ? await Number(body, ct) : 0;
            long n = await Number(body, ct);
            Files.Require(n > 0 && n <= m.TargetSize - produced, "Istruzione fuori limite.");
            operations++;
            if (op[0] == 1)
            {
                Files.Require(offset >= 0 && n <= m.SourceSize && offset <= m.SourceSize - n, "COPY fuori limite.");
                copied += n;
            }
            else if (op[0] == 3)
                zero += n;
            else
            {
                Files.Require(n <= BlockDeltaCodec.MaxLiteral, "Letterale troppo grande.");
                literal += n;
                long left = n;
                int tail = 0;
                while (left > 0)
                {
                    int read = (int)Math.Min(buffer.Length - 8, left);
                    await body.ReadExactlyAsync(buffer.AsMemory(tail, read), ct);
                    int length = tail + read;
                    for (int i = 0; i < length; i++)
                    {
                        var s = buffer.AsSpan(i);
                        if (i + 4 > tail && s.StartsWith(new byte[] { 0x7f, 69, 76, 70 }))
                            elf++;
                        if (i + 5 > tail && s.StartsWith("CD001"u8))
                            iso++;
                        if (i + 4 > tail && s.StartsWith("TIM2"u8))
                            tim2++;
                    }
                    tail = Math.Min(4, length);
                    buffer.AsSpan(length - tail, tail).CopyTo(buffer);
                    left -= read;
                }
            }
            produced += n;
        }
        return new
        {
            m.Algorithm,
            m.DiscId,
            m.PatchSize,
            m.SourceSize,
            m.TargetSize,
            operations,
            copiedBytes = copied,
            literalBytes = literal,
            zeroBytes = zero,
            literalMarkerOccurrences = new
            {
                ELF = elf,
                CD001 = iso,
                TIM2 = tim2
            },
            publicDistributionApproved = false,
            review = "Analisi strutturale e marcatori, non autorizzazione legale. I letterali possono contenere frammenti proprietari; nessun file viene esportato dall'audit."
        };
    }
}
