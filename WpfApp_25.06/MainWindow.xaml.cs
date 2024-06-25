using System;
using System.Collections.Generic;
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

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "UDP Server is not running.";
        }

        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            if (udpServer == null)
            {
                StartUDPServer();
                txtStatus.Text = "UDP Server with Client Quantity and Inactivity Timeout is running...";
                (sender as Button).IsEnabled = false;
                FindVisualChild<Button>(this, "StopServerButton").IsEnabled = true;
            }
            else
            {
                MessageBox.Show("Server is already running.");
            }
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            if (udpServer != null)
            {
                udpServer.Close();
                udpServer = null;
                txtStatus.Text = "UDP Server is not running.";
                (sender as Button).IsEnabled = false;
                FindVisualChild<Button>(this, "StartServerButton").IsEnabled = true;

                activeClients.Clear();
                lstClients.Items.Clear();
            }
            else
            {
                MessageBox.Show("Server is not running.");
            }
        }

        private void StartUDPServer()
        {
            udpServer = new UdpClient(12345);

            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        UdpReceiveResult result = await udpServer.ReceiveAsync();
                        ProcessClientMessage(result.Buffer, result.RemoteEndPoint);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in ReceiveMessages: {ex.Message}");
                }
            });

            Task.Run(() => CleanUpInactiveClients());
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

        private void CleanUpInactiveClients()
        {
            while (true)
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
                }

                Task.Delay(60000).Wait();
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
