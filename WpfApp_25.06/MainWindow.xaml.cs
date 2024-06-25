using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfApp_25._06
{
    public partial class MainWindow : Window
    {
        private UdpClient udpServer;
        private UdpClient udpClient;
        private bool isServerRunning;
        private readonly int port = 11000;

        private readonly int requestLimit = 10;
        private readonly TimeSpan requestLimitPeriod = TimeSpan.FromHours(1);

        private Dictionary<string, List<DateTime>> clientRequestLog;

        public MainWindow()
        {
            InitializeComponent();
            clientRequestLog = new Dictionary<string, List<DateTime>>();
        }

        private void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            StartServer();
        }

        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
        }

        private void SendRequestButton_Click(object sender, RoutedEventArgs e)
        {
            string products = ProductListTextBox.Text;
            if (!string.IsNullOrEmpty(products) && products != "Enter products separated by commas")
            {
                SendRequest(products);
            }
        }

        private void StartServer()
        {
            udpServer = new UdpClient(port);
            isServerRunning = true;
            Task.Run(() => ListenForRequests());
            StartServerButton.IsEnabled = false;
            StopServerButton.IsEnabled = true;
            ServerLogListBox.Items.Add("Server started...");
        }

        private void StopServer()
        {
            isServerRunning = false;
            udpServer.Close();
            StartServerButton.IsEnabled = true;
            StopServerButton.IsEnabled = false;
            ServerLogListBox.Items.Add("Server stopped.");
        }

        private async Task ListenForRequests()
        {
            while (isServerRunning)
            {
                try
                {
                    var result = await udpServer.ReceiveAsync();
                    string request = Encoding.UTF8.GetString(result.Buffer);
                    string clientEndpoint = result.RemoteEndPoint.ToString();

                    Dispatcher.Invoke(() => ServerLogListBox.Items.Add($"Received: {request} from {clientEndpoint}"));

                    string response;

                    if (IsRequestAllowed(clientEndpoint))
                    {
                        response = GetRecipes(request);
                        LogClientRequest(clientEndpoint);
                    }
                    else
                    {
                        response = "Request limit exceeded. Try again later.";
                    }

                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    await udpServer.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
                    Dispatcher.Invoke(() => ServerLogListBox.Items.Add($"Sent: {response}"));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => ServerLogListBox.Items.Add($"Error: {ex.Message}"));
                }
            }
        }

        private async Task SendRequest(string products)
        {
            if (udpClient == null)
            {
                udpClient = new UdpClient();
            }

            byte[] requestBytes = Encoding.UTF8.GetBytes(products);
            await udpClient.SendAsync(requestBytes, requestBytes.Length, "127.0.0.1", port);

            var result = await udpClient.ReceiveAsync();
            string response = Encoding.UTF8.GetString(result.Buffer);

            Dispatcher.Invoke(() => ClientResponseListBox.Items.Add(response));
        }

        private string GetRecipes(string products)
        {
            return $"Recipes for {products}: Recipe1, Recipe2, Recipe3";
        }

        private bool IsRequestAllowed(string clientEndpoint)
        {
            if (!clientRequestLog.ContainsKey(clientEndpoint))
            {
                clientRequestLog[clientEndpoint] = new List<DateTime>();
            }

            var requestTimes = clientRequestLog[clientEndpoint];
            DateTime now = DateTime.Now;

            requestTimes.RemoveAll(t => (now - t) > requestLimitPeriod);

            return requestTimes.Count < requestLimit;
        }

        private void LogClientRequest(string clientEndpoint)
        {
            if (!clientRequestLog.ContainsKey(clientEndpoint))
            {
                clientRequestLog[clientEndpoint] = new List<DateTime>();
            }

            clientRequestLog[clientEndpoint].Add(DateTime.Now);
        }

        private void ProductListTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ProductListTextBox.Text == "Enter products separated by commas")
            {
                ProductListTextBox.Text = "";
                ProductListTextBox.Foreground = Brushes.Black;
            }
        }

        private void ProductListTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProductListTextBox.Text))
            {
                ProductListTextBox.Text = "Enter products separated by commas";
                ProductListTextBox.Foreground = Brushes.Gray;
            }
        }
    }
}
