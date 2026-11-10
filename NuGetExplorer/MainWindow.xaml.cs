using System.Windows;
using NuGetExplorer.Services;
using NuGetExplorer.ViewModels;

namespace NuGetExplorer;

public partial class MainWindow : Window
{
    private const string FeedUrl = "https://pkgs.dev.azure.com/daikinapplied/_packaging/IT/nuget/v3/index.json";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new NuGetFeedService(FeedUrl), new InstalledPackageService());
    }
}
