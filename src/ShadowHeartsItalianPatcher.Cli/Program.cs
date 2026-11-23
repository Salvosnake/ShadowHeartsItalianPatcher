using System.Text.Json;
using ShadowHeartsItalianPatcher.Core;
using ShadowHeartsItalianPatcher.Infrastructure;
using System.Diagnostics;
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
var inspector = new IsoInspector();
var codec = new BlockDeltaCodec();
var catalog = new PatchCatalog();
var service = new PatchService(inspector, catalog, codec, new StorageSpace());
long last = -1;
var progress = new Progress<PatchProgress>(p => { long now = Environment.TickCount64; if (p.Percent == 100 || now - Interlocked.Read(ref last) > 1500) { Interlocked.Exchange(ref last, now); Console.Error.WriteLine($"{p.Percent:F1}% {p.Stage}"); } });
try
{
    if (args.Length == 0)
    {
        Help();
        return 2;
    }
    switch (args[0])
    {
        case "audit" when args.Length == 2:
            var packages = await catalog.LoadAsync(args[1], stop.Token);
            Files.Require(packages.Count == 1, "Indica la cartella di un solo pacchetto.");
            Console.WriteLine(JsonSerializer.Serialize(await PatchAudit.InspectAsync(packages[0], stop.Token), PatchCatalog.Json));
            return 0;
        case "inspect" when args.Length >= 2:
            var result = await service.InspectAsync(args[1], args.Length > 2 ? args[2] : Path.Combine(AppContext.BaseDirectory, "Patches"), progress, stop.Token);
            Console.WriteLine(JsonSerializer.Serialize(result, PatchCatalog.Json));
            return result.Package is null ? 3 : 0;
        case "apply" when args.Length is 3 or 4:
            var patches = args.Length == 4 ? args[3] : Path.Combine(AppContext.BaseDirectory, "Patches");
            var found = await service.InspectAsync(args[1], patches, progress, stop.Token);
            Files.Require(found.Package is not null, "Nessun pacchetto compatibile con l'hash di questa ISO. Il Disco2 richiede una ISO originale completa per generare la patch.");
            var timer = Stopwatch.StartNew();
            var file = await service.ApplyAsync(args[1], args[2], found.Package!, progress, stop.Token);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                file,
                seconds = timer.Elapsed.TotalSeconds,
                processPeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64
            }, PatchCatalog.Json));
            return 0;
        case "create" when args.Length >= 5:
            var package = await new PatchPackageBuilder(inspector, codec).BuildAsync(args[1], args[2], args[3], args[4], args.Length > 5 ? args[5] : "Candidata beta; collaudo del gioco incompleto.", progress, stop.Token);
            Console.WriteLine(JsonSerializer.Serialize(package, PatchCatalog.Json));
            return 0;
        default:
            Help();
            return 2;
    }
}
catch (OperationCanceledException) { Console.Error.WriteLine("Operazione annullata; eliminati soltanto i temporanei prodotti."); return 130; }
catch (Exception ex) when (ex is PatcherException or IOException or UnauthorizedAccessException or InvalidDataException or JsonException) { Console.Error.WriteLine("Errore: " + ex.Message); return 1; }
static void Help() => Console.WriteLine("Shadow Hearts Italian Patcher 1.0.0\naudit PACCHETTO\ninspect ISO [Patches]\napply ISO CARTELLA_DESTINAZIONE [Patches]\ncreate ISO_ORIGINALE ISO_ITALIANA NUOVO_PACCHETTO VERSIONE [COMPATIBILITÀ]");
