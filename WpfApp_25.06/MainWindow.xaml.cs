using System.Net.Sockets;
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

namespace WpfApp_25._06
{
    public partial class MainWindow : Window
    {
        private UdpClient udpServer;
        private bool isRunning;
        private readonly int port = 11000;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            StartServer();
        }

        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
        }

        private void StartServer()
        {
            udpServer = new UdpClient(port);
            isRunning = true;
            Task.Run(() => ListenForRequests());
            StartServerButton.IsEnabled = false;
            StopServerButton.IsEnabled = true;
            LogListBox.Items.Add("Server started...");
        }

        private void StopServer()
        {
            isRunning = false;
            udpServer.Close();
            StartServerButton.IsEnabled = true;
            StopServerButton.IsEnabled = false;
            LogListBox.Items.Add("Server stopped.");
        }

        private async Task ListenForRequests()
        {
            while (isRunning)
            {
                try
                {
                    var result = await udpServer.ReceiveAsync();
                    string request = Encoding.UTF8.GetString(result.Buffer);
                    Dispatcher.Invoke(() => LogListBox.Items.Add($"Received: {request}"));

                    string response = GetRecipes(request);
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                    await udpServer.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
                    Dispatcher.Invoke(() => LogListBox.Items.Add($"Sent: {response}"));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => LogListBox.Items.Add($"Error: {ex.Message}"));
                }
            }
        }

        private string GetRecipes(string products)
        {
            return $"Recipes for {products}: Recipe1, Recipe2, Recipe3";
        }
    }
}
