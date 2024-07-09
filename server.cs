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

                while (true)
                {
                    Console.WriteLine("Waiting connection ... ");
                    Socket clientSocket = listener.Accept();
                    Console.WriteLine("Client connected.");

                    // Receive <SOH>
                    byte[] sohBuffer = new byte[1];
                    clientSocket.Receive(sohBuffer);
                    if (sohBuffer[0] != 0x01)
                    {
                        Console.WriteLine("Invalid message received after SOH");
                        continue;
                    }

                    // Send <ACK> after <SOH>
                    var ackMessage = "\x06";

                    clientSocket.Send(Encoding.ASCII.GetBytes(ackMessage));
                    
                    Console.WriteLine($"Socket server kirim acknowledgment: \"{(ackMessage == "\x06" ? "<ACK>" : ackMessage)}\"");
                    

                    while (true)
                    {
                        // Receive <STX>...<ETX>
                        byte[] messageBuffer = new byte[1024];
                        int bytesReceived = clientSocket.Receive(messageBuffer);
                        string data = Encoding.ASCII.GetString(messageBuffer, 0, bytesReceived);
                        var stx = "\x02";
                        var etx = "\x03";
                        if (data.StartsWith(stx) && data.EndsWith(etx))
                        {
                            data = data.Substring(1, data.Length - 2); // Remove <STX> and <ETX>
                            // Console.WriteLine($"Pesan diterima -> {0} \"{(stx == "\x02" ? "<STX>" : stx)}\"", data);
                            // Console.WriteLine($"Socket server terima pesan: \"{(stx == "\x02" ? "<STX>" : stx)}\" ...\"{(etx == "\x03" ? "<ETX>" : etx)}\" ");
                            // Send <ACK> after <STX>...<ETX>
                            Console.WriteLine($"Socket server terima pesan: \"{(data.StartsWith("\x02") ? "<STX>" : "")}{data}{(data.EndsWith("\x03") ? "<ETX>" : "")}\"");

                            
                            clientSocket.Send(Encoding.ASCII.GetBytes(ackMessage));
                            Console.WriteLine($"Socket server kirim acknowledgment:  \"{(ackMessage == "\x06" ? "<ACK>" : ackMessage)}\"");

                        }

                        var eot = "\x04";
                        if (data.Contains(eot))
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
