using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;
using CyclerSim.Services;
using CyclerSim.ViewModels;
using System.ComponentModel.Design;


namespace CyclerSim
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Services
                    services.AddSingleton<IHttpService, HttpService>();
                    services.AddSingleton<IDataService, DataService>();

                    // ViewModels
                    services.AddTransient<MainViewModel>();

                    // Logging
                    services.AddLogging(builder => builder.AddConsole());
                })
                .Build();

            var mainWindow = new MainWindow();
            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
            mainWindow.DataContext = mainViewModel;

            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            base.OnExit(e);
        }
    }

}
