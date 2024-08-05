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
            Console.Write("Masukkan IP Address listen: ");
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
            Console.Write("Masukkan port listen: ");
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

                Console.WriteLine("Menunggu koneksi...");
                Socket clientSocket = listener.Accept();
                Console.WriteLine("Client terhubung.");

                while (true)
                {
                    try
                    {
                        ServerSendMessage(clientSocket);

                        if (!ServerReceiveMessage(clientSocket))
                        {
                            break;
                        }
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

        private static void ServerSendMessage(Socket clientSocket)
        {
            Console.Write("Masukkan pesan untuk dikirimkan ke client: ");
            string userMessage = Console.ReadLine();

            if (userMessage.ToLower() == "exit")
            {
                return;
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
                Console.WriteLine("Gagal menerima SCK setelah SOH ");
                return;
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
                Console.WriteLine($"Socket server kirim pesan: \"{(stx == "\x02" ? "<STX>" : stx)}{Encoding.ASCII.GetString(chunkBuffer)}{(isLastChunk ? "<ETX>" : "<ETB>")}\"");

                clientSocket.Send(messageToSend);

                // Wait for <ACK>
                clientSocket.Receive(ackBuffer);

                if (ackBuffer[0] != 0x06)
                {
                    Console.WriteLine($"Gagal menerima ACK setelah mengirimkan potongan, mulai dari byte ke {i}");
                    return;
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

        private static bool ServerReceiveMessage(Socket clientSocket)
        {
            Console.WriteLine("Menunggu untuk menerima pesan dari clinet...");

            byte[] sohBuffer = new byte[1];
            clientSocket.Receive(sohBuffer);
            if (sohBuffer[0] != 0x01)
            {
                Console.WriteLine("SOH yang diterima dari client tidak valid.");
                return false;
            }

            var ackMessage = "\x06";
            clientSocket.Send(Encoding.ASCII.GetBytes(ackMessage));
            Console.WriteLine($"Socket server kirim ACK: \"{(ackMessage == "\x06" ? "<ACK>" : ackMessage)}");

            List<byte> finalMsgBuff = new List<byte>();
            while (true)
            {
                byte[] messageBuffer = new byte[1024];
                int byteReceived = clientSocket.Receive(messageBuffer);
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

                    clientSocket.Send(Encoding.ASCII.GetBytes(ackMessage));
                    Console.WriteLine($"Socket server kirim acknowledgment akhir: \"{(ackMessage == "\x06" ? "<ACK>" : ackMessage)}\"");

                    finalMsgBuff.RemoveRange(0, endIdk + 1); // Clear buffer untuk menerima message selanjutnya
                }

                if (finalMsgBuff.Contains(0x04))
                {
                    Console.WriteLine("Akhir penerimaan transmisi dari client.");
                    finalMsgBuff.Clear();
                    break;
                }

            }
            return true;
        }
    }
}
