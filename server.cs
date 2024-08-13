using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Server
{
    class Program
    {
        private static Socket listener;
        private static bool isListening;
        private static Socket clientHandler;
        private enum ServerState { SendingState, ReceiveState}
        private static ServerState currentState = ServerState.SendingState;
        static void Main(string[] args)
        {
            StartServer();
        }
        static void StartServer()
        {
            Console.Write("Masukkan IP Address listener: ");
            string ipAddressInput = Console.ReadLine();
            IPAddress ipAddr;
            if (!IPAddress.TryParse(ipAddressInput, out ipAddr))
            {
                Console.WriteLine("IP Address tidak valid");
                return;
            }

            Console.Write("Masukkan port listener: ");
            string portInput = Console.ReadLine();
            int port;

            if (!int.TryParse(portInput, out port) || port <= 0 || port > 65535)
            {
                Console.WriteLine("Port tidak valid.");
                return;
            }

            IPEndPoint localEndPoint = new IPEndPoint(ipAddr, port);

            try
            {
                // Initialize the listener socket
                listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                listener.Bind(localEndPoint);
                listener.Listen(10);

                Console.WriteLine("Menunggu koneksi dari klien...");
                isListening = true;

                while (isListening)
                {
                    clientHandler = listener.Accept();
                    Console.WriteLine("Klien terhubung!");
                    try
                    {
                    Thread sendThread = new Thread(ServerSendMessage);
                    Thread receiveThread = new Thread(ServerReceiveMessage);

                    sendThread.Start();
                    receiveThread.Start();

                    sendThread.Join();
                    receiveThread.Join();

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unexpected exception: {0}", e.ToString());
                    }   
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected exception: {e.Message}");
                Thread.Sleep(2000);
            }
            finally
            {
                listener?.Close();
            }
        }

        private static void ServerReceiveMessage()
        {
            // AutoResetEvent canReceive = new AutoResetEvent(true);
            try
            {
                while (true)
                {
                    if (currentState != ServerState.ReceiveState)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    // canReceive.WaitOne();
                
                    byte[] sohBuffer = new byte[1];
                    int bytesRec = clientHandler.Receive(sohBuffer);
                    if (bytesRec == 0 || sohBuffer[0] != 0x01)
                    {
                        Console.WriteLine("SOH yang diterima dari klien tidak valid");
                        return;
                    }

                    var ackMessage = "\x06";
                    clientHandler.Send(Encoding.ASCII.GetBytes(ackMessage));
                    Console.WriteLine($"Socket server kirim ACK: \"{(ackMessage == "\x06" ? "<ACK>" : ackMessage)}\"");

                    List<byte> finalMsgBuff = new List<byte>();
                    while (true)
                    {
                        byte[] messageBuffer = new byte[1024];
                        int byteReceived = clientHandler.Receive(messageBuffer);
                        if (byteReceived == 0) break;

                        for (int i = 0; i < byteReceived; i++)
                        {
                            finalMsgBuff.Add(messageBuffer[i]);
                        }

                        int stxIdk = finalMsgBuff.IndexOf(0x02);
                        int etbIdk = finalMsgBuff.IndexOf(0x23);
                        int etxIdk = finalMsgBuff.IndexOf(0x03);

                        if (stxIdk != -1 && (etbIdk != -1 || etxIdk != -1))
                        {
                            int endIdk = etbIdk != -1 ? etbIdk : etxIdk;
                            byte[] chunkBytes = finalMsgBuff.GetRange(stxIdk + 1, endIdk - stxIdk - 1).ToArray();
                            string chunkMessage = Encoding.ASCII.GetString(chunkBytes);

                            if (etbIdk != -1)
                            {
                                Console.WriteLine($"Socket server terima potongan: \"<STX>{chunkMessage}<ETB>\"");
                            }
                            else if (etxIdk != -1)
                            {
                                Console.WriteLine($"Socket server terima potongan terakhir \"<STX>{chunkMessage}<ETX>\"");
                            }

                            clientHandler.Send(Encoding.ASCII.GetBytes(ackMessage));
                            Console.WriteLine($"Socket server kirim ACK: \"{(ackMessage == "\x06" ? "<ACK>" : ackMessage)}\"");

                            finalMsgBuff.RemoveRange(0, endIdk + 1);
                        }

                        if (finalMsgBuff.Contains(0x04))
                        {
                            Console.WriteLine("Akhir penerimaan transmisi dari klien.");
                            finalMsgBuff.Clear();
                            break;
                        }                
                    }
                    // currentState = ServerState.SendingState;
                    // canReceive.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
            }
            finally
            {
                clientHandler?.Shutdown(SocketShutdown.Both);
                clientHandler?.Close();
            }
        }

        private static void ServerSendMessage()
        {
            // AutoResetEvent canSend = new AutoResetEvent(true);
            if (clientHandler == null) return;

            while (true)
            {
                if (currentState != ServerState.SendingState) continue;
                
                // canSend.WaitOne();

                // Console.Write("Masukkan pesan untuk dikirimkan ke klien: ");
                // string serverMessage = Console.ReadLine();
                string serverMessage = "Halo, Client!";

                if (serverMessage.ToLower() == "exit")
                {
                    isListening = false;
                    listener.Close();
                    return;
                }

                var soh = "\x01";
                var stx = "\x02";
                var etb = "\x23";
                var etx = "\x03";
                var eot = "\x04";

                clientHandler.Send(Encoding.ASCII.GetBytes(soh));
                Console.WriteLine($"Socket server kirim: \"{(soh == "\x01" ? "<SOH>" : soh)}\"");

                byte[] ackBuffer = new byte[1];
                clientHandler.Receive(ackBuffer);

                if (ackBuffer[0] != 0x06)
                {
                    Console.WriteLine("Gagal menerima ACK setelah SOH");
                    return;
                }
                else
                {
                    Console.WriteLine("Socket server terima ACK");
                }

                byte[] messageBuffer = Encoding.ASCII.GetBytes(serverMessage);
                int bufferSize = 255;

                for (int i = 0; i < messageBuffer.Length; i += bufferSize)
                {
                    bool isLastChunk = i + bufferSize >= messageBuffer.Length;
                    int chunkSize = isLastChunk ? messageBuffer.Length - i : bufferSize;
                    byte[] chunkBuffer = new byte[chunkSize];
                    Array.Copy(messageBuffer, i, chunkBuffer, 0, chunkSize);

                    string chunkMessage = stx + Encoding.ASCII.GetString(chunkBuffer) + (isLastChunk ? etx : etb);
                    byte[] messageToSend = Encoding.ASCII.GetBytes(chunkMessage);
                    Console.WriteLine($"Socket server kirim pesan: \"{(stx == "\x02" ? "<STX>" : stx)}{Encoding.ASCII.GetString(chunkBuffer)}{(isLastChunk ? "<ETX>" : "<ETB>")}\"");

                    clientHandler.Send(messageToSend);

                    clientHandler.Receive(ackBuffer);

                    if (ackBuffer[0] != 0x06)
                    {
                        Console.WriteLine($"Gagal menerima ACK setelah mengirim chunk dimulai dari byte {i}");
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Socket server terima ACK");
                    }
                }

                clientHandler.Send(Encoding.ASCII.GetBytes(eot));
                Console.WriteLine($"Socket server kirim: \"{(eot == "\x04" ? "<EOT>" : eot)}\"");
                currentState = ServerState.ReceiveState;
                // canSend.Set();
            }
        }
    }
}
