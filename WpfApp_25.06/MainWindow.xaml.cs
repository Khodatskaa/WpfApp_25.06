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
using Newtonsoft.Json;

namespace WpfApp_25._06
{
    public partial class MainWindow : Window
    {
        private UdpClient udpServer;
        private Dictionary<string, DateTime> activeClients = new Dictionary<string, DateTime>();
        private const int MaxClients = 5;
        private const int InactivityTimeoutMinutes = 10;
        private const int RequestLimit = 10;
        private const int HourlyLimitMinutes = 60;
        private static Dictionary<string, Queue<DateTime>> requestQueue = new Dictionary<string, Queue<DateTime>>();
        private const string LogFilePath = "server_log.txt";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            StartServer();
        }

        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            StartServer();
            txtServerStatus.Text = "UDP Server with Client Quantity and Inactivity Timeout is running...";
            (sender as Button).IsEnabled = false;
            FindVisualChild<Button>(this, "StopServerButton").IsEnabled = true;
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            StopServer();
            txtServerStatus.Text = "UDP Server is not running.";
            (sender as Button).IsEnabled = false;
            FindVisualChild<Button>(this, "StartServerButton").IsEnabled = true;
        }

        private void StartServer()
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

        private void StopServer()
        {
            if (udpServer != null)
            {
                udpServer.Close();
                udpServer = null;
                activeClients.Clear();
                lstClients.Items.Clear();
            }
        }

        private void ProcessClientMessage(byte[] data, IPEndPoint clientEP)
        {
            string clientRequest = Encoding.ASCII.GetString(data);

            ManageClientActivity(clientEP.Address.ToString());

            if (CheckClientQuantity() && CheckRequestRateLimit(clientEP.Address.ToString()))
            {
                string recipeName = clientRequest.Trim();
                string recipeText = GetRecipeText(recipeName);
                string imagePath = GetImagePath(recipeName);

                if (recipeText != null && imagePath != null)
                {
                    RecipeResponse response = new RecipeResponse
                    {
                        RecipeName = recipeName,
                        RecipeText = recipeText,
                        ImageData = File.ReadAllBytes(imagePath)
                    };

                    SendRecipeResponse(response, clientEP);

                    LogMessage($"Sent recipe and image for {recipeName} to {clientEP.Address}");
                }
                else
                {
                    string errorMessage = $"Recipe '{recipeName}' not found.";
                    byte[] errorData = Encoding.ASCII.GetBytes(errorMessage);
                    udpServer.Send(errorData, errorData.Length, clientEP);

                    LogMessage($"Recipe '{recipeName}' not found. Error message sent to {clientEP.Address}");
                }
            }
        }

        private string GetRecipeText(string recipeName)
        {
            switch (recipeName.ToLower())
            {
                case "caesar salad":
                    return "Caesar Salad Recipe:\n- Romaine lettuce\n- Croutons\n- Parmesan cheese\n- Caesar dressing";
                case "spaghetti carbonara":
                    return "Spaghetti Carbonara Recipe:\n- Spaghetti\n- Eggs\n- Pancetta\n- Parmesan cheese\n- Black pepper";
                default:
                    return null;
            }
        }

        private string GetImagePath(string recipeName)
        {
            switch (recipeName.ToLower())
            {
                case "caesar salad":
                    return "Images/caesar_salad.jpg";
                case "spaghetti carbonara":
                    return "Images/spaghetti_carbonara.jpg";
                default:
                    return null;
            }
        }

        private void SendRecipeResponse(RecipeResponse response, IPEndPoint clientEP)
        {
            byte[] responseData = SerializeObject(response);
            udpServer.Send(responseData, responseData.Length, clientEP);
        }

        private byte[] SerializeObject(object obj)
        {
            string json = JsonConvert.SerializeObject(obj);
            return Encoding.UTF8.GetBytes(json);
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
            CleanUpRequestQueue();

            if (!requestQueue.ContainsKey(clientAddress))
            {
                requestQueue[clientAddress] = new Queue<DateTime>();
            }

            requestQueue[clientAddress].Enqueue(DateTime.Now);

            return requestQueue[clientAddress].Count <= RequestLimit;
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

    public class RecipeResponse
    {
        public string RecipeName { get; set; }
        public string RecipeText { get; set; }
        public byte[] ImageData { get; set; }
    }
}
