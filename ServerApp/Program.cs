using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpChatServer
{
    class Program
    {
        private static TcpListener _server;
        private static ConcurrentDictionary<string, TcpClient> _connectedClients = new ConcurrentDictionary<string, TcpClient>();
        private static ConcurrentDictionary<string, string> _userCredentials = new ConcurrentDictionary<string, string>(); // Username and password storage
        private static StringBuilder _logBuilder = new StringBuilder();
        private static long _totalBytesSent = 0;
        private static long _totalBytesReceived = 0;

        static void Main(string[] args)
        {
            LoadUserCredentials(); // Load users 

            Console.SetOut(new ConsoleWriter(_logBuilder)); // Redirect console output to a StringBuilder

            _server = new TcpListener(IPAddress.Any, 8888);
            _server.Start();
            Console.WriteLine("Server started on port 8888.");

            Task.Run(() => AcceptClientsAsync());

            Console.WriteLine("Press Enter to shut down the server.");
            Console.ReadLine();
            ShutdownServer();
        }

        private static void LoadUserCredentials()
        {
            // Placeholder user credentials for testing
            _userCredentials.TryAdd("user1", "1");
            _userCredentials.TryAdd("user2", "2");
            _userCredentials.TryAdd("user3", "3");
        }

        private static async Task AcceptClientsAsync()
        {
            while (true)
            {
                try
                {
                    var client = await _server.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            var stream = client.GetStream();
            var buffer = new byte[1024];
            string clientUsername = null;

            try
            {
                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;
                    Interlocked.Add(ref _totalBytesReceived, bytesRead); // Increment total bytes received in the session

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string[] parts = message.Split('|');

                    if (parts.Length > 0)
                    {
                        string command = parts[0].ToUpper();

                        switch (command)
                        {
                            case "LOGIN":
                                if (parts.Length == 3)
                                {
                                    string username = parts[1];
                                    string password = parts[2];

                                    if (_userCredentials.ContainsKey(username) && _userCredentials[username] == password)
                                    {
                                        clientUsername = username;
                                        if (_connectedClients.TryAdd(username, client))
                                        {
                                            await SendMessage(stream, "SUCCESS");
                                            Console.WriteLine($"[{username}] logged in.");
                                        }
                                        else
                                        {
                                            await SendMessage(stream, "ALREADY");
                                            Console.WriteLine($"[{username}] already logged in.");
                                            client.Close();
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        await SendMessage(stream, "INVALID");
                                        Console.WriteLine($"[{username}] login failed.");
                                        client.Close();
                                        return;
                                    }
                                }
                                break;

                            case "LOGOUT":
                                if (!string.IsNullOrEmpty(clientUsername))
                                {
                                    Console.WriteLine($"[{clientUsername}] logged out.");
                                    _connectedClients.TryRemove(clientUsername, out _);
                                    client.Close();
                                    return;
                                }
                                break;

                            case "MESSAGE":
                                if (parts.Length == 3 && !string.IsNullOrEmpty(clientUsername))
                                {
                                    string receiver = parts[1];
                                    string msgContent = parts[2];
                                    await RouteMessage(clientUsername, receiver, msgContent);
                                }
                                break;

                            case "FILE":
                                if (parts.Length == 4 && !string.IsNullOrEmpty(clientUsername))
                                {
                                    string receiver = parts[1];
                                    string fileName = parts[2];
                                    long fileSize = long.Parse(parts[3]);
                                    await HandleFileTransfer(stream, clientUsername, receiver, fileName, fileSize);
                                }
                                break;

                            case "/LOG":
                                string logFileName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"; // Log file path
                                string logFilePath = Path.Combine(Environment.CurrentDirectory, logFileName);
                                await File.WriteAllTextAsync(logFilePath, _logBuilder.ToString());
                                string fullPath = Path.GetFullPath(logFilePath).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                                Console.WriteLine($"Log command executed by [{clientUsername}]. Log saved to {logFilePath}.");
                                await SendMessage(stream, $"Log command executed. Log saved to {logFilePath}.");
                                break;

                            case "/CLOSE":
                                ShutdownServer();
                                break;

                            case "/TIME":
                                Console.WriteLine($"Time sent to {clientUsername}.");
                                await SendMessage(stream, $"Server time is: {DateTime.Now}");
                                break;

                            case "/STATISTIC":
                                int onlineUsers = _connectedClients.Count;
                                long totalBytes = _totalBytesSent + _totalBytesReceived;
                                await SendMessage(stream, $"Online users: {onlineUsers}.\nTotal bytes sent: {_totalBytesSent} bytes.\nTotal bytes received: {_totalBytesReceived} bytes");
                                Console.WriteLine($"Statistics sent to [{clientUsername}].");
                                break;

                            case "/HELP":
                                await SendMessage(stream, "Available commands:\n/log - Get server log in this session\n/CLOSE - Shutdown server\n/TIME - Get server time\n/STATISTIC - Get server statistic\n/HELP - Show available commands");
                                Console.WriteLine($"[{clientUsername}] asked for commands.");
                                break;

                            default:
                                await SendMessage(stream, "Unknown command.");
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client error: {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(clientUsername))
                {
                    _connectedClients.TryRemove(clientUsername, out _);
                    Console.WriteLine($"{clientUsername} disconnected.");
                }
            }
        }

        private static async Task RouteMessage(string sender, string receiver, string message)
        {
            if (_connectedClients.TryGetValue(receiver, out TcpClient receiverClient))
            {
                var stream = receiverClient.GetStream();
                string fullMessage = $"[{DateTime.Now:G}] [{sender}]: {message}";
                await SendMessage(stream, fullMessage);
                Console.WriteLine($"Message from [{sender}] to [{receiver}]: {message}");
            }
            else
            {
                Console.WriteLine($"Receiver [{receiver}] is not online or not exist.");
                await SendMessage(_connectedClients[sender].GetStream(), $"Receiver [{receiver}] is not online or not exist.");
            }
        }

        private static async Task HandleFileTransfer(NetworkStream senderStream, string sender, string receiver, string fileName, long fileSize)
        {
            if (_connectedClients.TryGetValue(receiver, out TcpClient receiverClient))
            {
                var receiverStream = receiverClient.GetStream();

                //Send file information to receiver
                await SendMessage(receiverStream, $"FILE|{sender}|{fileName}|{fileSize}");

                //Forward file content
                byte[] buffer = new byte[8192];
                long remainingBytes = fileSize;
                while (remainingBytes > 0)
                {
                    int bytesToRead = (int)Math.Min(buffer.Length, remainingBytes);
                    int bytesRead = await senderStream.ReadAsync(buffer, 0, bytesToRead);
                    if (bytesRead == 0) break;

                    await receiverStream.WriteAsync(buffer, 0, bytesRead);
                    remainingBytes -= bytesRead;
                    Interlocked.Add(ref _totalBytesReceived, bytesRead);
                }

                Console.WriteLine($"File from [{sender}] to [{receiver}]: {fileName}");
                await SendMessage(_connectedClients[sender].GetStream(), $"FILESENT|{receiver}|{fileName}");
            }
            else
            {
                Console.WriteLine($"Receiver [{receiver}] is not online or not exist.");
                await SendMessage(_connectedClients[sender].GetStream(), $"FILEERROR|Receiver [{receiver}] is not online or not exist.");
            }
        }

        private static async Task SendMessage(NetworkStream stream, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(data, 0, data.Length);
            Interlocked.Add(ref _totalBytesSent, data.Length);
        }

        private static void ShutdownServer()
        {
            Console.WriteLine("Shutting down server...");
            foreach (var client in _connectedClients.Values)
            {
                client.Close();
            }
            _connectedClients.Clear();
            _server.Stop();
            Environment.Exit(0);
        }

        private class ConsoleWriter : TextWriter
        {
            private StringBuilder _logBuilder;
            private TextWriter _originalOut;

            public ConsoleWriter(StringBuilder logBuilder)
            {
                _logBuilder = logBuilder;
                _originalOut = Console.Out;
            }

            public override void Write(string? value)
            {
                string timestampedValue = $"[{DateTime.Now:G}] {value}";
                _logBuilder.Append(timestampedValue);
                _originalOut.Write(timestampedValue);
            }

            public override void WriteLine(string? value)
            {
                string timestampedValue = $"[{DateTime.Now:G}] {value}";
                _logBuilder.AppendLine(timestampedValue);
                _originalOut.WriteLine(timestampedValue);
            }

            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}
