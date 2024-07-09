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
            IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddr = ipHost.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddr, 11111);

            Socket listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                Console.WriteLine("Waiting connection ... ");
                Socket clientSocket = listener.Accept();
                Console.WriteLine("Client connected.");

                while (true)
                {
                    

                    // Receive <SOH>
                    byte[] sohBuffer = new byte[1];
                    clientSocket.Receive(sohBuffer);
                    if (sohBuffer[0] != 0x01)
                    {
                        var nak = "\x21";
                        clientSocket.Send(Encoding.ASCII.GetBytes(nak));
                        Console.WriteLine($"Invalid message received :");

                        continue;
                    }

                    // Send <ACK> after <SOH>
                    var ackMessage = "\x06";

                    clientSocket.Send(Encoding.ASCII.GetBytes(ackMessage));
                    
                    Console.WriteLine($"Socket server kirim acknowledgment pertama: \"{(ackMessage == "\x06" ? "<ACK>" : ackMessage)}\"");
                    

                    List<byte> finalMsgBuffer = new List<byte>();
                    while (true)
                    {
                        // Receive data
                        byte[] messageBuffer = new byte[1024];
                        int bytesReceived = clientSocket.Receive(messageBuffer);
                        for (int i = 0; i < bytesReceived; i++)
                        {
                            finalMsgBuffer.Add(messageBuffer[i]);
                        }

                        // Check if message ends with <ETX>
                        
                            int stxIndex = finalMsgBuffer.IndexOf(0x02); // STX
                            int etbIndex = finalMsgBuffer.IndexOf(0x23); // ETB
                            int etxIndex = finalMsgBuffer.IndexOf(0x03); // ETX

                            if (stxIndex != -1 && (etbIndex != -1 || etxIndex != -1))
                            {
                                int endIndex = etbIndex != -1 ? etbIndex : etxIndex;
                                byte[] chunkBytes = finalMsgBuffer.GetRange(stxIndex + 1, endIndex - stxIndex - 1).ToArray();
                                string chunkMessage = Encoding.ASCII.GetString(chunkBytes);

                                if (etbIndex != -1)
                                {
                                    Console.WriteLine($"Socket server terima potongan: \"<STX>{chunkMessage}<ETB>\"");
                                }
                                else if (etxIndex != -1)
                                {
                                    Console.WriteLine($"Socket server terima potongan terakhir: \"<STX>{chunkMessage}<ETX>\"");
                                }
                                // Kirim <ACK> setelah kirim chunk
                                clientSocket.Send(Encoding.ASCII.GetBytes(ackMessage));
                                Console.WriteLine($"Socket server kirim acknowledgment akhir: \"{(ackMessage == "\x06" ? "<ACK>" : ackMessage)}\"");

                                finalMsgBuffer.RemoveRange(0, endIndex + 1); // Clear buffer for next message
                            }
                        

                        // Check if message contains <EOT>
                        if (finalMsgBuffer.Contains(0x04)) // EOT
                        {
                            Console.WriteLine("End of Transmission received");
                            break;
                        }
                    }

                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
