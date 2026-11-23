using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using ShadowHeartsItalianPatcher.Core;
namespace ShadowHeartsItalianPatcher.Infrastructure;

public sealed class IsoInspector : IIsoInspector
{
    const int Sector = 2048;
    static uint U32(byte[] b, int p) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p, 4));
    static uint B32(byte[] b, int p) => BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(p, 4));
    static void Extent(byte[] b, int p, long length)
    {
        Files.Require(U32(b, p) == B32(b, p + 4), "ISO corrotta: indirizzo del filesystem incoerente.");
        Files.Require(U32(b, p + 8) == B32(b, p + 12), "ISO corrotta: dimensione del filesystem incoerente.");
        Files.Require((ulong)U32(b, p) * Sector + U32(b, p + 8) <= (ulong)length, "ISO corrotta: risorsa fuori dai limiti.");
    }
    static async Task<byte[]> Read(Stream s, long at, int size, CancellationToken ct)
    {
        Files.Require(at >= 0 && size >= 0 && at <= s.Length - size, "ISO troncata o puntatore fuori dai limiti.");
        byte[] b = new byte[size];
        s.Position = at;
        await s.ReadExactlyAsync(b, ct);
        return b;
    }
    public async Task<IsoIdentity> InspectAsync(Stream s, CancellationToken ct)
    {
        Files.Require(s.CanSeek && s.Length >= 20 * Sector && s.Length % Sector == 0, "File non valido: serve una ISO a settori da 2048 byte.");
        var pvd = await Read(s, 16L * Sector, Sector, ct);
        Files.Require(pvd[0] == 1 && Encoding.ASCII.GetString(pvd, 1, 5) == "CD001" && pvd[6] == 1, "Filesystem ISO9660 non riconosciuto o ISO corrotta.");
        Files.Require(BinaryPrimitives.ReadUInt16LittleEndian(pvd.AsSpan(128)) == Sector && BinaryPrimitives.ReadUInt16BigEndian(pvd.AsSpan(130)) == Sector, "Dimensione dei settori ISO non supportata.");
        Files.Require(U32(pvd, 80) == B32(pvd, 84) && (ulong)U32(pvd, 80) * Sector <= (ulong)s.Length, "ISO troncata: volume oltre la fine del file.");
        Extent(pvd, 158, s.Length);
        uint rootSize = U32(pvd, 166);
        Files.Require(rootSize > 0 && rootSize <= 16 * 1024 * 1024, "Directory ISO non valida.");
        var dir = await Read(s, (long)U32(pvd, 158) * Sector, (int)rootSize, ct);
        var entries = new Dictionary<string, (long Offset, int Size)>();
        for (int i = 0; i < dir.Length;)
        {
            ct.ThrowIfCancellationRequested();
            int len = dir[i];
            if (len == 0)
            {
                i = (i / Sector + 1) * Sector;
                continue;
            }
            Files.Require(len >= 34 && i + len <= dir.Length && i % Sector + len <= Sector, "Directory ISO corrotta.");
            int nameLen = dir[i + 32];
            Files.Require(33 + nameLen <= len, "Nome ISO fuori dai limiti.");
            Extent(dir, i + 2, s.Length);
            string name = Encoding.ASCII.GetString(dir, i + 33, nameLen).Split(';')[0];
            uint size = U32(dir, i + 10);
            if (name is "SYSTEM.CNF" or "SLUS_210.41" or "SLUS_210.44")
            {
                Files.Require(size <= 16 * 1024 * 1024, "File di avvio ISO anomalo.");
                Files.Require(entries.TryAdd(name, ((long)U32(dir, i + 2) * Sector, (int)size)), "Directory ISO ambigua.");
            }
            i += len;
        }
        Files.Require(entries.TryGetValue("SYSTEM.CNF", out var cnf), "SYSTEM.CNF assente: versione del gioco non supportata.");
        Files.Require(cnf.Size > 0 && cnf.Size <= 65536, "SYSTEM.CNF non valido.");
        string text = Encoding.ASCII.GetString(await Read(s, cnf.Offset, cnf.Size, ct));
        var boot = Regex.Match(text, @"(?im)^\s*BOOT2\s*=\s*cdrom0:\\?([^;\s]+);1\s*$");
        Files.Require(boot.Success, "BOOT2 non riconosciuto in SYSTEM.CNF.");
        string id = boot.Groups[1].Value.ToUpperInvariant();
        Files.Require(id is "SLUS_210.41" or "SLUS_210.44", "Versione non supportata: serve Shadow Hearts: Covenant USA, Disco 1 o 2. PAL e giapponese non sono compatibili.");
        Files.Require(Regex.IsMatch(text, @"(?im)^\s*VMODE\s*=\s*NTSC\s*$"), "Regione o modalità video non supportata: serve USA NTSC.");
        Files.Require(entries.TryGetValue(id, out var exe) && exe.Size >= 52, "Eseguibile originale assente o troncato.");
        var elf = await Read(s, exe.Offset, 4, ct);
        Files.Require(elf.AsSpan().SequenceEqual(new byte[] { 0x7f, 69, 76, 70 }), "Eseguibile PS2 non valido.");
        return new(id, id.EndsWith("41") ? 1 : 2, "USA / NTSC", s.Length);
    }
}
