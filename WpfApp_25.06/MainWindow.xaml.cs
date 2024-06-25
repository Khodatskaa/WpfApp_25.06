using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace WpfApp_25._06
{
    public partial class MainWindow : Window
    {
        UdpClient udpClient;

        public MainWindow()
        {
            InitializeComponent();

            udpClient = new UdpClient();
        }

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string serverIP = txtServerIP.Text;
                int serverPort = Convert.ToInt32(txtServerPort.Text);

                string message = txtMessageToSend.Text;
                byte[] data = Encoding.ASCII.GetBytes(message);

                udpClient.Send(data, data.Length, serverIP, serverPort);

                IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] responseData = udpClient.Receive(ref serverEP);
                string responseMessage = Encoding.ASCII.GetString(responseData);

                txtReceivedMessage.Text = $"Server Response: {responseMessage}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
