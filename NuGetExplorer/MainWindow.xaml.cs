using NuGetExplorer.Services;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NuGetExplorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var feedService = new NuGetFeedService("https://pkgs.dev.azure.com/daikinapplied/_packaging/IT/nuget/v3/index.json");

            var packages = await feedService.SearchPackagesAsync("");

            // Get versions of a selected package
            var versions = await feedService.GetPackageVersionsAsync("BaseCadFiles.DataCenterAHU");

            Console.WriteLine(versions);
        }
    }
}