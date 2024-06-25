using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace WpfApp_25._06
{
    public partial class MainWindow : Window
    {
        private UdpClient udpServer;
        private Dictionary<string, Queue<DateTime>> requestQueue = new Dictionary<string, Queue<DateTime>>();
        private const int RequestLimit = 10;
        private const int HourlyLimitMinutes = 60;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            StartUDPServer();
        }

        private void StartUDPServer()
        {
            udpServer = new UdpClient(12345); 
            txtStatus.Text = "UDP Server with Request Throttling is running...";

            udpServer.BeginReceive(ReceiveCallback, null);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = udpServer.EndReceive(ar, ref clientEP);
            string clientRequest = Encoding.ASCII.GetString(data);

            if (CheckRequestRateLimit(clientEP.Address.ToString()))
            {
                string responseMessage = $"Server received: {clientRequest}";
                byte[] responseData = Encoding.ASCII.GetBytes(responseMessage);
                udpServer.Send(responseData, responseData.Length, clientEP);
                Console.WriteLine($"Response sent to {clientEP}");
            }
            else
            {
                Console.WriteLine($"Request from {clientEP} exceeded rate limit. Ignoring request.");
            }

            udpServer.BeginReceive(ReceiveCallback, null);
        }

        private bool CheckRequestRateLimit(string clientAddress)
        {
            CleanUpRequestQueue();

            if (!requestQueue.ContainsKey(clientAddress))
            {
                requestQueue[clientAddress] = new Queue<DateTime>();
            }

            requestQueue[clientAddress].Enqueue(DateTime.Now);

            return requestQueue[clientAddress].Count <= RequestLimit;
        }

        private void CleanUpRequestQueue()
        {
            foreach (var clientAddress in requestQueue.Keys)
            {
                while (requestQueue[clientAddress].Count > 0 && (DateTime.Now - requestQueue[clientAddress].Peek()).TotalMinutes > HourlyLimitMinutes)
                {
                    requestQueue[clientAddress].Dequeue();
                }

                if (requestQueue[clientAddress].Count == 0)
                {
                    requestQueue.Remove(clientAddress);
                }
            }
        }
    }
}
