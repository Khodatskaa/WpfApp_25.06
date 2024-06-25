using System;
using System.Collections.Generic;
using System.IO;
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
        private Dictionary<string, DateTime> activeClients = new Dictionary<string, DateTime>();
        private const int MaxClients = 5;
        private const int InactivityTimeoutMinutes = 10;
        private const string LogFilePath = "server_log.txt";
        private bool serverRunning;

        public MainWindow()
        {
            InitializeComponent();
            InitializeLogFile();
        }

        private void InitializeLogFile()
        {
            try
            {
                using (StreamWriter writer = File.AppendText(LogFilePath))
                {
                    writer.WriteLine($"===== Server started at {DateTime.Now} =====");
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error initializing log file: {ex.Message}");
            }
        }

        private void LogMessage(string message)
        {
            try
            {
                using (StreamWriter writer = File.AppendText(LogFilePath))
                {
                    writer.WriteLine($"[{DateTime.Now}] {message}");
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtServerStatus.Text = "UDP Server is not running.";
        }

        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            StartServer();
            txtServerStatus.Text = "UDP Server with Client Quantity and Inactivity Timeout is running...";
            (sender as Button).IsEnabled = false;
            FindVisualChild<Button>(this, "StopServerButton").IsEnabled = true;
            LogMessage("Server started.");
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
            txtServerStatus.Text = "UDP Server is not running.";
            (sender as Button).IsEnabled = false;
            FindVisualChild<Button>(this, "StartServerButton").IsEnabled = true;
            LogMessage("Server stopped.");
        }

        private void ManageServer_Click(object sender, RoutedEventArgs e)
        {
            if (serverRunning)
            {
                StopServer();
            }
            else
            {
                StartServer();
            }
        }

        private async void StartServer()
        {
            if (udpServer == null)
            {
                try
                {
                    udpServer = new UdpClient(12345);

                    await Task.Run(async () =>
                    {
                        serverRunning = true;
                        try
                        {
                            while (udpServer != null)
                            {
                                UdpReceiveResult result = await udpServer.ReceiveAsync();
                                ProcessClientMessage(result.Buffer, result.RemoteEndPoint);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // UdpClient has been disposed, exit the loop
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Error in ReceiveMessages: {ex.Message}");
                        }
                        finally
                        {
                            serverRunning = false;
                        }
                    });

                    await Task.Run(() => CleanUpInactiveClients());
                }
                catch (Exception ex)
                {
                    LogMessage($"Error starting UDP server: {ex.Message}");
                    StopServer();
                }
            }
        }

        private void StopServer()
        {
            if (udpServer != null)
            {
                udpServer.Close();
                udpServer = null;
                activeClients.Clear();
                lstClients.Items.Clear();
                serverRunning = false;
            }
        }

        private void ProcessClientMessage(byte[] data, IPEndPoint clientEP)
        {
            string clientRequest = Encoding.ASCII.GetString(data);

            ManageClientActivity(clientEP.Address.ToString());

            if (CheckClientQuantity() && CheckRequestRateLimit(clientEP.Address.ToString()))
            {
                string responseMessage = $"Server received: {clientRequest}";
                byte[] responseData = Encoding.ASCII.GetBytes(responseMessage);
                udpServer.Send(responseData, responseData.Length, clientEP);

                Dispatcher.Invoke(() =>
                {
                    if (!lstClients.Items.Contains(clientEP.Address.ToString()))
                    {
                        lstClients.Items.Add(clientEP.Address.ToString());
                    }
                });

                LogMessage($"Request from {clientEP.Address} processed: {clientRequest}");
            }
        }

        private void ManageClientActivity(string clientAddress)
        {
            activeClients[clientAddress] = DateTime.Now;
        }

        private bool CheckClientQuantity()
        {
            return activeClients.Count < MaxClients;
        }

        private bool CheckRequestRateLimit(string clientAddress)
        {
            return true; 
        }

        private async Task CleanUpInactiveClients()
        {
            while (udpServer != null)
            {
                List<string> inactiveClients = new List<string>();

                foreach (var clientAddress in activeClients.Keys)
                {
                    if ((DateTime.Now - activeClients[clientAddress]).TotalMinutes > InactivityTimeoutMinutes)
                    {
                        inactiveClients.Add(clientAddress);
                    }
                }

                foreach (var clientAddress in inactiveClients)
                {
                    activeClients.Remove(clientAddress);

                    Dispatcher.Invoke(() =>
                    {
                        if (lstClients.Items.Contains(clientAddress))
                        {
                            lstClients.Items.Remove(clientAddress);
                        }
                    });

                    LogMessage($"Client {clientAddress} disconnected due to inactivity.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1)); 
            }
        }

        private T FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T && child.GetValue(NameProperty).ToString() == childName)
                {
                    return (T)child;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child, childName);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }
    }
}
