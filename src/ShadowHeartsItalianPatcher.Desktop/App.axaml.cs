using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ShadowHeartsItalianPatcher.Core;
using ShadowHeartsItalianPatcher.Infrastructure;
namespace ShadowHeartsItalianPatcher.Desktop;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var services = new ServiceCollection().AddSingleton<IIsoInspector, IsoInspector>().AddSingleton<IPatchCatalog, PatchCatalog>().AddSingleton<IDeltaCodec, BlockDeltaCodec>().AddSingleton<IStorageSpace, StorageSpace>().AddSingleton<IPatchService, PatchService>().AddSingleton<IFileDialogs>(new FileDialogs(window)).AddSingleton<MainViewModel>().BuildServiceProvider();
            var model = services.GetRequiredService<MainViewModel>();
            window.DataContext = model;
            window.Closing += (_, e) => { if (model.IsBusy) { e.Cancel = true; model.Cancel(); } };
            desktop.MainWindow = window;
            window.Opened += async (_, _) =>
            {
                var args = desktop.Args ?? [];
                model.PropertyChanged += (_, e) => { if (args.Contains("--diagnostics") && e.PropertyName == "Status") Console.WriteLine(model.Status); };
                for (int i = 0; i + 1 < args.Length; i++)
                {
                    if (args[i] == "--destination")
                        model.SetOutputDirectory(args[++i]);
                    else if (args[i] == "--iso")
                        await model.LoadIsoAsync(args[++i]);
                }
                if (args.Contains("--dark"))
                    model.DarkTheme = true;
                if (args.Contains("--generate") && model.GenerateCommand.CanExecute(null))
                    model.GenerateCommand.Execute(null);
            };
            desktop.Exit += (_, _) => { model.Dispose(); services.Dispose(); };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
