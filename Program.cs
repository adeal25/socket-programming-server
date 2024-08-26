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
        private static bool isRunning = true;
        private static bool SedangMengirim = false;
        private static Socket clientHandler;
 
        private static EventWaitHandle sendHandle = new AutoResetEvent(true);
        private static EventWaitHandle receiveHandle = new AutoResetEvent(true);

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
                    Thread thInputUser = new Thread(ServerSendMessage);
                    Thread thMainSocket = new Thread(ServerReceiveMessage);

                    thInputUser.Start();
                    thMainSocket.Start();

                    thInputUser.Join();
                    thMainSocket.Join();

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
            if (clientHandler == null) return;
            try
            {
                while (isRunning)
                {
                    // receiveHandle.WaitOne();

                    
                    byte[] receiveBuffer = new byte[2048];
                    int byteReceived = clientHandler.Receive(receiveBuffer);
                    for (int i = 0; i<byteReceived; i++)
                    {
                        if (SedangMengirim == true)
                        {
                            if (receiveBuffer[i] == 0x06)
                            {
                                Console.WriteLine("Socket server terima ACK");
                                sendHandle.Set();
                            }
                        }
                        else
                        {
                            if (receiveBuffer[i] == 0x01)
                            {
                                Console.WriteLine("SOH diterima");
                                receiveHandle.Set();
                                clientHandler.Send(new byte[] {0x06});
                                Console.WriteLine("Socket server kirim ACK");

                                List<byte> finalMsgBuff = new List<byte>();

                                while (true)
                                {
                                    byte[] messageBuffer = new byte[1024];
                                    int msgByteReceived = clientHandler.Receive(messageBuffer);
                                    for (int j = 0; j < msgByteReceived; j++)
                                    {
                                        finalMsgBuff.Add(messageBuffer[j]);
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
                                            Console.WriteLine($"Socket client terima potongan pesan: \"<STX>{chunkMessage}<ETB>\"");
                                        }
                                        else if (etxIdk != -1)
                                        {
                                            Console.WriteLine($"Socket client terima potongan pesan terakhir \"<STX>{chunkMessage}<ETX>\"");
                                        }
            
                                        clientHandler.Send(new byte[] {0x06});
                                        Console.WriteLine("Socket client kirim ACK");
            
                                        finalMsgBuff.RemoveRange(0, endIdk + 1);
                                    }
            
                                    if (finalMsgBuff.Contains(0x04))
                                    {
                                        Console.WriteLine("Akhir penerimaan transmisi dari server.");
                                        finalMsgBuff.Clear();
                                        break;
                                    }
                                }
                            }
                        }
                    }
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
            if (clientHandler == null) return;

            while (isRunning)
            {
                

                // sendHandle.WaitOne();
                
                Console.Write("Masukkan pesan untuk dikirimkan ke klien: ");
                string serverMessage = Console.ReadLine();
                // string serverMessage = "Halo, Client!";

                if (serverMessage.ToLower() == "exit")
                {
                    isListening = false;
                    listener.Close();
                    isRunning = false;
                    return;
                }
                SedangMengirim = true;

                byte[] soh = new byte[] { 0x01 };
                clientHandler.Send(soh);
                Console.WriteLine("Socket server kirim: <SOH>");

                sendHandle.WaitOne();
                
                
                byte[] messageBuffer = Encoding.ASCII.GetBytes(serverMessage);
                int bufferSize = 255;

                for (int i = 0; i < messageBuffer.Length; i += bufferSize)
                {
                    bool isLastChunk = i + bufferSize >= messageBuffer.Length;
                    int chunkSize = isLastChunk ? messageBuffer.Length - i : bufferSize;
                    byte[] chunkBuffer = new byte[chunkSize];
                    Array.Copy(messageBuffer, i, chunkBuffer, 0, chunkSize);

                    string chunkMessage = Encoding.ASCII.GetString(chunkBuffer);
                    byte[] messageToSend = Encoding.ASCII.GetBytes($"\x02{chunkMessage}{(isLastChunk? "\x03" : "\x23")}");
                    Console.WriteLine($"Socket server kirim pesan: <STX>{chunkMessage}{(isLastChunk ? "<ETX>" : "<ETB>")}");

                    clientHandler.Send(messageToSend);

                    // kodingan untuk send ackBuffer
                    sendHandle.WaitOne();

                    // clientHandler.Send(Encoding.ASCII.GetBytes(ackMessage));
                    // Console.WriteLine($"Socket server kirim: \"{(ackMessage == "\x06" ? "<ACK>" : ackMessage)}\"");
                    // =============

                }

                clientHandler.Send(new byte[] { 0x04 });
                Console.WriteLine($"Socket server kirim: <EOT>");
                SedangMengirim = false;
                // currentState = ServerState.Receiving;
                // receiveHandle.Set();
            
            }
        }
    }
}