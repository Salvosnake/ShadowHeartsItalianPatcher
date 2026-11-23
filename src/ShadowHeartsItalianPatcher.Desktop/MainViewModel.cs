using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ShadowHeartsItalianPatcher.Core;
using Avalonia;
using Avalonia.Styling;
namespace ShadowHeartsItalianPatcher.Desktop;

public sealed class AsyncCommand(Func<Task> execute, Func<bool>? enabled = null) : ICommand
{
    bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => !running && (enabled?.Invoke() ?? true);
    public async void Execute(object? p)
    {
        if (!CanExecute(p))
            return;
        running = true;
        Changed();
        try
        {
            await execute();
        }
        finally { running = false; Changed(); }
    }
    public void Changed() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    readonly IPatchService service; readonly IFileDialogs dialogs; CancellationTokenSource? cancellation; Inspection? inspection;
    string isoPath = "", destination = "", disc = "Nessun disco selezionato", region = "—", verification = "Seleziona la tua ISO originale USA", translation = "—", required = "—", status = "Pronto", log = "Funzionamento offline. Originali sempre conservati.\n"; double percent; bool busy, dark;
    public MainViewModel(IPatchService service, IFileDialogs dialogs)
    {
        this.service = service;
        this.dialogs = dialogs;
        SelectIsoCommand = new(SelectIso, () => !IsBusy);
        SelectDestinationCommand = new(SelectDestination, () => !IsBusy);
        GenerateCommand = new(Generate, () => !IsBusy && inspection?.Package is not null && Directory.Exists(Destination));
        CancelCommand = new(() => { Cancel(); return Task.CompletedTask; }, () => IsBusy);
        OpenFolderCommand = new(() => { try { dialogs.OpenFolder(Destination); } catch (Exception e) { Append(e.Message); } return Task.CompletedTask; }, () => Directory.Exists(Destination));
    }
    public string VersionText => $"Applicazione {PatcherVersion.Current} · Offline";
    public string PatchesDirectory => Environment.GetEnvironmentVariable("SHIP_PATCHES") ?? Path.Combine(AppContext.BaseDirectory, "Patches");
    public string IsoPath
    {
        get => isoPath; private set => Set(ref isoPath, value);
    }
    public string Destination
    {
        get => destination; private set
        {
            Set(ref destination, value);
            CommandsChanged();
        }
    }
    public string Disc
    {
        get => disc; private set => Set(ref disc, value);
    }
    public string Region
    {
        get => region; private set => Set(ref region, value);
    }
    public string Verification
    {
        get => verification; private set => Set(ref verification, value);
    }
    public string Translation
    {
        get => translation; private set => Set(ref translation, value);
    }
    public string Required
    {
        get => required; private set => Set(ref required, value);
    }
    public string Status
    {
        get => status; private set => Set(ref status, value);
    }
    public string Log
    {
        get => log; private set => Set(ref log, value);
    }
    public double Percent
    {
        get => percent; private set
        {
            Set(ref percent, value);
            OnPropertyChanged(nameof(PercentText));
        }
    }
    public string PercentText => $"{Percent:F1}%";
    public bool IsBusy
    {
        get => busy; private set
        {
            Set(ref busy, value);
            CommandsChanged();
        }
    }
    public bool DarkTheme
    {
        get => dark; set
        {
            Set(ref dark, value);
            if (Application.Current is { } a)
                a.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
    public AsyncCommand SelectIsoCommand
    {
        get;
    }
    public AsyncCommand SelectDestinationCommand
    {
        get;
    }
    public AsyncCommand GenerateCommand
    {
        get;
    }
    public AsyncCommand CancelCommand
    {
        get;
    }
    public AsyncCommand OpenFolderCommand
    {
        get;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        OnPropertyChanged(name);
    }
    void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new(name));
    void CommandsChanged()
    {
        SelectIsoCommand?.Changed();
        SelectDestinationCommand?.Changed();
        GenerateCommand?.Changed();
        CancelCommand?.Changed();
        OpenFolderCommand?.Changed();
    }
    void Append(string text)
    {
        Log = $"{Log}[{DateTime.Now:HH:mm:ss}] {text}\n";
        if (Log.Length > 24000)
            Log = Log[^20000..];
    }
    IProgress<PatchProgress> Progress() => new Progress<PatchProgress>(p => { Percent = p.Percent; if (Status != p.Stage) { Status = p.Stage; Append(p.Stage); } });
    async Task Run(Func<CancellationToken, Task> operation)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        Percent = 0;
        cancellation = new();
        try
        {
            await operation(cancellation.Token);
        }
        catch (OperationCanceledException) { Status = "Operazione annullata"; Append("Annullato. La ISO originale è conservata; eliminati soltanto i temporanei prodotti."); }
        catch (Exception e) { Status = "Operazione non completata"; Verification = "Verifica o generazione non riuscita"; Append(e is PatcherException ? e.Message : $"{e.GetType().Name}: {e.Message}"); }
        finally { cancellation.Dispose(); cancellation = null; IsBusy = false; }
    }
    async Task SelectIso()
    {
        string? path;
        try
        {
            path = await dialogs.SelectIso();
        }
        catch (Exception e) { Append(e.Message); return; }
        if (path is null)
            return;
        await LoadIsoAsync(path);
    }
    public void SetOutputDirectory(string path) => Destination = Path.GetFullPath(path);
    public async Task LoadIsoAsync(string path)
    {
        IsoPath = path;
        inspection = null;
        Disc = "Verifica in corso…";
        Region = "—";
        Translation = "—";
        Required = "—";
        await Run(async ct =>
        {
            var reporter = Progress();
            inspection = await Task.Run(() => service.InspectAsync(path, PatchesDirectory, reporter, ct), ct);
            Disc = $"Disco {inspection.Iso.DiscNumber} · {inspection.Iso.DiscId}";
            Region = inspection.Iso.Region;
            Append($"SHA-256 originale: {inspection.Iso.Sha256}");
            if (inspection.Package is { } package)
            {
                Verification = "ISO originale compatibile · SHA-256 verificato";
                Translation = package.Manifest.TranslationVersion;
                Required = $"{(package.Manifest.TargetSize + 16 * 1024 * 1024) / 1073741824.0:F2} GiB";
                Append(package.Manifest.Compatibility);
                Status = "Pronto per generare una nuova ISO";
            }
            else
            {
                Verification = "Layout/hash non supportato o pacchetto assente";
                Status = "Nessuna patch compatibile";
                Append("Il disco è riconosciuto, ma nessun pacchetto corrisponde a dimensione e SHA-256. Una ISO modificata non è accettata. Per il Disco2 serve il pacchetto creato dalla ISO originale completa.");
            }
        });
    }
    async Task SelectDestination()
    {
        try
        {
            var path = await dialogs.SelectFolder();
            if (path is not null)
                Destination = path;
        }
        catch (Exception e) { Append(e.Message); }
    }
    async Task Generate()
    {
        var package = inspection?.Package;
        if (package is null)
            return;
        await Run(async ct => { var reporter = Progress(); var result = await Task.Run(() => service.ApplyAsync(IsoPath, Destination, package, reporter, ct), ct); Append($"Creato e verificato: {result}"); Append($"SHA-256 italiano: {package.Manifest.TargetSha256}"); Status = "ISO italiana generata e verificata"; });
    }
    public void Cancel() => cancellation?.Cancel(); public void Dispose() => cancellation?.Dispose();
}
