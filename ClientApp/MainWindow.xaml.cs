using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace TcpChatApp
{
    public partial class MainWindow : Window
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private bool _isConnected = false;
        private bool _shouldStop = false;

        public MainWindow()
        {
            InitializeComponent();
            UpdateUIState();
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text;
            string password = txtPassword.Password;

            if (!_isConnected)
            {
                await ConnectToServer();
                bool isLoginSuccessful = await LoginAsync(username, password);


                if (isLoginSuccessful)
                {

                }
                else
                {
                    await LogoutFromServer();
                }
                UpdateUIState();
            }
            else
            {
                await LogoutFromServer();
            }
        }

        private async Task<bool> LoginAsync(string username, string password)
        {
            try
            {
                if (_stream == null || !_isConnected)
                {
                    MessageBox.Show("Connection is not established.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                string loginMessage = $"LOGIN|{username}|{password}";
                byte[] loginData = Encoding.UTF8.GetBytes(loginMessage);
                await _stream.WriteAsync(loginData, 0, loginData.Length);

                byte[] responseData = new byte[1024];
                int bytesRead = await _stream.ReadAsync(responseData, 0, responseData.Length);
                string responseMessage = Encoding.UTF8.GetString(responseData, 0, bytesRead);

                if (responseMessage.Contains("SUCCESS"))
                {
                    _isConnected = true;
                    lblStatus.Text = $"{username} is logged in";
                    UpdateUIState();
                    return true;
                }
                else if (responseMessage.Contains("ALREADY"))
                {
                    MessageBox.Show("You already logged in", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                else
                {
                    MessageBox.Show("Invalid username or password", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error logging in: {ex.Message}", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }


        private async Task ConnectToServer()
        {
            try
            {
                string ipAddress = txtIpAddress.Text;
                int port = int.Parse(txtPort.Text);

                _client = new TcpClient();
                await _client.ConnectAsync(ipAddress, port);
                _stream = _client.GetStream();
                _isConnected = true;

                // Start listening for messages from the server
                _ = Task.Run(ReceiveMessages);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to server: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LogoutFromServer()
        {
            await SendMessage("LOGOUT");
            DisconnectFromServer();
            UpdateUIState();
        }

        private void DisconnectFromServer()
        {
            _shouldStop = true;
            if (_client != null)
            {
                _client.Close();
                _client = null;
            }
            _isConnected = false;
            lblStatus.Text = "Disconnected";
        }

        private void UpdateUIState()
        {
            btnLogin.Content = _isConnected ? "Logout" : "Login";
            txtIpAddress.IsEnabled = !_isConnected;
            txtPort.IsEnabled = !_isConnected;
            txtUsername.IsEnabled = !_isConnected;
            txtPassword.IsEnabled = !_isConnected;
            txtReceiver.IsEnabled = _isConnected;
            btnSend.IsEnabled = _isConnected;
            btnTransferFile.IsEnabled = _isConnected;
            statusLed.Fill = _isConnected ? Brushes.Green : Brushes.Red;
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnected && !string.IsNullOrEmpty(txtMessage.Text) && !string.IsNullOrEmpty(txtReceiver.Text))
            {
                string message = txtMessage.Text;
                if (message.StartsWith("/"))
                {
                    await HandleCommand(message);
                }
                else
                {
                    string fullMessage = $"MESSAGE|{txtReceiver.Text}|{message}";
                    await SendMessage(fullMessage);
                }
                txtMessage.Clear();
            }
            else
            {
                txtReceived.Text += $"[{DateTime.Now:G}] Message must not blank. Receiver's username must be specified.\n";
            }
        }

        private async Task HandleCommand(string message)
        {
            string command = message.ToLower();
            switch (command)
            {
                case "/clear":
                    txtReceived.Text = "";
                    break;

                default:
                    await SendMessage(message);
                    break;
            }
        }

        private async Task SendMessage(string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                await _stream.WriteAsync(data, 0, data.Length);
                Console.WriteLine($"Sent: {message}");
            }
            catch (Exception ex)
            {
                DisconnectFromServer();
                UpdateUIState();
                MessageBox.Show($"Error sending message: {ex.Message}", "Send Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SendMessage(byte[] data)
        {
            try
            {
                await _stream.WriteAsync(data, 0, data.Length);
                Console.WriteLine($"Sent: {data}");
            }
            catch (Exception ex)
            {
                DisconnectFromServer();
                UpdateUIState();
                MessageBox.Show($"Error sending message: {ex.Message}", "Send Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ReceiveMessages()
        {
            byte[] buffer = new byte[1024];
            while (_isConnected && !_shouldStop)
            {
                try
                {
                    if (_stream.DataAvailable)
                    {
                        int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            Console.WriteLine($"Received: {receivedMessage}");
                            string[] parts = receivedMessage.Split('|');

                            //Check if the message is a file message format
                            if (parts[0] == "FILE")
                            {
                                string sender = parts[1];
                                string fileName = parts[2];
                                long fileSize = long.Parse(parts[3]);

                                Dispatcher.Invoke(() =>
                                {
                                    txtReceived.Text += $"[{DateTime.Now:G}] Receiving file from {sender}...\n";
                                });

                                string savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

                                using (FileStream fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                                {
                                    long remainingBytes = fileSize;
                                    while (remainingBytes > 0)
                                    {
                                        int bytesToRead = (int)Math.Min(remainingBytes, buffer.Length);
                                        int bytesReadFile = await _stream.ReadAsync(buffer, 0, bytesToRead);
                                        if (bytesReadFile == 0)
                                        {
                                            break;
                                        }
                                        await fileStream.WriteAsync(buffer, 0, bytesReadFile);
                                        remainingBytes -= bytesReadFile;
                                    }
                                }

                                //File received message
                                Dispatcher.Invoke(() =>
                                {
                                    txtReceived.Text += $"[{DateTime.Now:G}] File received from {sender} and saved to {savePath}\n";
                                });
                            }
                            else if (parts[0] == "FILESENT")
                            {
                                // FILESENT|receiver|fileName
                                string receiver = parts[1];
                                string fileName = parts[2];
                                Dispatcher.Invoke(() =>
                                {
                                    txtReceived.Text += $"[{DateTime.Now:G}] File sent to [{receiver}]: {fileName}\n";
                                });
                            }
                            else if (parts[0] == "FILEERROR")
                            {
                                // FILEERROR|errorMessage
                                Dispatcher.Invoke(() =>
                                {
                                    txtReceived.Text += $"[{DateTime.Now:G}] File error: {parts[1]}\n";
                                });
                            }
                            else
                            //This is a normal message
                            {
                                // Use Dispatcher to update the UI from the main thread
                                Dispatcher.Invoke(() =>
                                {
                                    txtReceived.Text += receivedMessage + "\n";
                                });
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(100); // Prevent too much CPU usage
                    }
                }
                catch (Exception ex)
                {
                    if (!_shouldStop)
                    // Connection error
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _isConnected = false;
                            DisconnectFromServer();
                            UpdateUIState();
                            MessageBox.Show($"Connection error: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    break;
                }
            }
            //// If we're here, it means the connection was closed
            //if (!_shouldStop)
            //{
            //    Dispatcher.Invoke(() =>
            //    {
            //        DisconnectFromServer();
            //        UpdateUIState();
            //        MessageBox.Show("The connection to the server has been closed.", "Disconnected", MessageBoxButton.OK, MessageBoxImage.Information);
            //    });
            //}
        }

        private async void btnTransferFile_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected) return;
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;

                // Gửi thông tin file
                string fileInfo = $"FILE|{txtReceiver.Text}|{fileName}|{fileSize}";
                await SendMessage(fileInfo);

                // Đợi một chút để đảm bảo thông tin file được gửi đi trước
                await Task.Delay(100);

                // Gửi nội dung file
                using (FileStream fs = File.OpenRead(filePath))
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await _stream.WriteAsync(buffer, 0, bytesRead);
                    }
                }

                txtReceived.Text += $"File sent: {fileName}\n";
            }
        }
    }
}
