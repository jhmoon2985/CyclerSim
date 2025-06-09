using CyclerSim.ViewModels;
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

namespace CyclerSim
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ToggleAllAutoUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                var currentState = viewModel.Channels.FirstOrDefault()?.AutoUpdate ?? false;
                var newState = !currentState;

                foreach (var channel in viewModel.Channels)
                {
                    channel.AutoUpdate = newState;
                }

                var statusMessage = newState ? "All channels auto-update enabled" : "All channels auto-update disabled";
                viewModel.StatusMessage = statusMessage;
            }
        }
    }
}