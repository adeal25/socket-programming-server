using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            ExecuteServer();
        }

        public static void ExecuteServer()
        {
            Console.Write("Masukkan IP Address server: ");
            string ipAddressInput = Console.ReadLine();
            IPAddress ipAddr;
            // IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
            // IPAddress ipAddr = ipHost.AddressList[0];
            // IPEndPoint localEndPoint = new IPEndPoint(ipAddr, 11111);
            if (!IPAddress.TryParse(ipAddressInput, out ipAddr))
            {
                Console.WriteLine("IP Address tidak valid");
                return;
            }
            Console.Write("Masukkan port server: ");
            string portInput = Console.ReadLine();
            int port;

            if (!int.TryParse(portInput, out port) || port <= 0 || port > 65535)
            {
                Console.WriteLine("Port tidak valid.");
                return;
    
            }

            IPEndPoint localEndPoint = new IPEndPoint(ipAddr, port);
            
            Socket listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                Console.WriteLine("Waiting for a connection...");
                Socket clientSocket = listener.Accept();
                Console.WriteLine("Client connected.");

                while (true)
                {
                    try
                    {
                        Console.Write("Enter the message to send to the client: ");
                        string userMessage = Console.ReadLine();

                        if (userMessage.ToLower() == "exit")
                        {
                            break;
                        }

                        var soh = "\x01";
                        var ack = "\x06";
                        var stx = "\x02";
                        var etb = "\x23";
                        var etx = "\x03";
                        var eot = "\x04";

                        // Send <SOH>
                        clientSocket.Send(Encoding.ASCII.GetBytes(soh));
                        Console.WriteLine($"Socket server kirim: \"{(soh == "\x01" ? "<SOH>" : soh)}\"");

                        // Wait for <ACK>
                        byte[] ackBuffer = new byte[1];
                        clientSocket.Receive(ackBuffer);

                        if (ackBuffer[0] != 0x06)
                        {
                            Console.WriteLine("Failed to receive ACK after SOH");
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("Socket server terima ACK");
                        }

                        // Send message in chunks
                        byte[] messageBuffer = Encoding.ASCII.GetBytes(userMessage);
                        int bufferSize = 255;

                        for (int i = 0; i < messageBuffer.Length; i += bufferSize)
                        {
                            bool isLastChunk = i + bufferSize >= messageBuffer.Length;
                            int chunkSize = isLastChunk ? messageBuffer.Length - i : bufferSize;
                            byte[] chunkBuffer = new byte[chunkSize];
                            Array.Copy(messageBuffer, i, chunkBuffer, 0, chunkSize);

                            string chunkMessage = stx + Encoding.ASCII.GetString(chunkBuffer) + (isLastChunk ? etx : etb);
                            byte[] messageToSend = Encoding.ASCII.GetBytes(chunkMessage);
                            Console.WriteLine($"Socket server kirim pesan: \"{(stx == "\x02" ? "<STX>" : stx)}\"{Encoding.ASCII.GetString(chunkBuffer)}{(isLastChunk ? "<ETX>" : "<ETB>")}\"");

                            clientSocket.Send(messageToSend);

                            // Wait for <ACK>
                            clientSocket.Receive(ackBuffer);

                            if (ackBuffer[0] != 0x06)
                            {
                                Console.WriteLine($"Failed to receive ACK after sending chunk starting at byte {i}");
                                continue;
                            }
                            else
                            {
                                Console.WriteLine("Socket server terima ACK");
                            }
                        }

                        // Send <EOT>
                        clientSocket.Send(Encoding.ASCII.GetBytes(eot));
                        Console.WriteLine($"Socket server kirim: \"{(eot == "\x04" ? "<EOT>" : eot)}\"");
                    }
                    catch (SocketException se)
                    {
                        Console.WriteLine($"SocketException: {se.Message}");
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Exception: {e.Message}");
                        break;
                    }
                }

                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
