

# TCP Chat App

## Author
gnaohuv

## Overview
This project is a chat application developed using the **TCP protocol** for communication and a **Windows Presentation Foundation (WPF)** interface for the user experience. This project developed for an [assignment](https://github.com/user-attachments/files/16648132/Assignment_02_Hoan.docx) from instructor HoanNN in FPT University (Summer 2024, B3W). This can't even satisfy him though...

## Features
- **Chat between clients**: Allows multiple clients to communicate with each other.
- **File transfer**: Supports sending files between clients.
- **Commands**: Includes commands like `/log`, `/help`, `/time`, `/clear`, and `/statistic`.
- **Interface**: Simple and functional, though not highly responsive.

## Architecture
The application follows a client-server architecture, with the following components:

### Server
- **TCP Listener**: Listens for incoming client connections.
- **Client Handler**: Manages connected clients, including message broadcasting and file transfers.
- **Command Processor**: Handles various commands sent by clients.
- **Logging**: Records chat logs and server events.

### Client
- **TCP Client**: Connects to the server and handles communication.
- **User Interface**: Built with WPF, providing a chat window and file transfer options.
- **Command Interface**: Allows users to send commands to the server.
- **File Transfer**: Manages sending and receiving files.

### Communication
- **Message Protocol**: Defines the format for messages and commands exchanged between clients and the server.
- **File Transfer Protocol**: Manages the segmentation and reassembly of files during transfer.

## Getting Started
### Prerequisites
- .NET Framework
- Visual Studio

### Installation
1. Clone the repository:
   ```sh
   git clone https://github.com/gnaohuv22/TCPChatApp.git
   ```
2. Open the solution in Visual Studio.
3. Build the project.

### Usage
1. Run the `ServerApp` to start the server. For localhost, it will be hosted at 127.0.0.1:8888
2. Run the `ClientApp` to connect to the server, log in with username and password then start communicating.

## Contributing
Feel free to fork this repository and submit pull requests.

## License
This project is licensed under the MIT License.

---

Generated by Copilot
