using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

class RemoteCursor
{
    // ===================== WIN API =====================
    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT lpPoint);

    struct POINT
    {
        public int X;
        public int Y;
    }

    // ===================== GLOBAL ======================
    static bool allowControl = false;
    static string securityCode;
    static bool running = true;

    const int TCP_PORT = 9000;
    const int UDP_PORT = 9001;

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("RemoteCursor.exe receiver");
            Console.WriteLine("RemoteCursor.exe sender");
            return;
        }

        if (args[0].ToLower() == "receiver")
            StartReceiver();
        else if (args[0].ToLower() == "sender")
            StartSender();
        else
            Console.WriteLine("Unknown mode");
    }

    // ================= RECEIVER =====================

    static void StartReceiver()
    {
        securityCode = new Random().Next(100000, 999999).ToString();

        Console.WriteLine("=== RECEIVER MODE ===");
        Console.WriteLine("Security Code: " + securityCode);
        Console.WriteLine("Give this code to Sender");
        Console.WriteLine("Press SPACE to stop control\n");

        new Thread(TcpAuthServer).Start();
        new Thread(UdpControlListener).Start();
        new Thread(EmergencyStop).Start();

        while (running)
            Thread.Sleep(1000);
    }

    static void TcpAuthServer()
    {
        TcpListener server = new TcpListener(IPAddress.Any, TCP_PORT);
        server.Start();

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[64];
            stream.Read(buffer, 0, buffer.Length);
            string code = Encoding.UTF8.GetString(buffer).Trim('\0');

            if (code == securityCode)
            {
                allowControl = true;
                stream.Write(Encoding.UTF8.GetBytes("OK"));
                Console.WriteLine("Sender authenticated!");
            }
            else
            {
                stream.Write(Encoding.UTF8.GetBytes("DENIED"));
            }

            client.Close();
        }
    }

    static void UdpControlListener()
    {
        UdpClient udp = new UdpClient(UDP_PORT);
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            byte[] data = udp.Receive(ref ep);

            if (!allowControl)
                continue;

            string msg = Encoding.UTF8.GetString(data);
            string[] parts = msg.Split('|');

            int x = int.Parse(parts[0]);
            int y = int.Parse(parts[1]);

            SetCursorPos(x, y);
        }
    }

    static void EmergencyStop()
    {
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var k = Console.ReadKey(true);
                if (k.Key == ConsoleKey.Spacebar)
                {
                    allowControl = false;
                    Console.WriteLine("Connection terminated by Receiver!");
                }
            }
        }
    }

    // ================= SENDER =====================

    static void StartSender()
    {
        Console.WriteLine("=== SENDER MODE ===");

        Console.Write("Receiver IP: ");
        string ip = Console.ReadLine();

        Console.Write("Security Code: ");
        string code = Console.ReadLine();

        try
        {
            TcpClient tcp = new TcpClient();
            tcp.Connect(ip, TCP_PORT);
            NetworkStream stream = tcp.GetStream();
            stream.Write(Encoding.UTF8.GetBytes(code));

            byte[] buffer = new byte[64];
            stream.Read(buffer, 0, buffer.Length);
            string response = Encoding.UTF8.GetString(buffer).Trim('\0');

            if (response != "OK")
            {
                Console.WriteLine("Access denied");
                return;
            }

            Console.WriteLine("Connected! Controlling cursor...");

            UdpClient udp = new UdpClient();
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ip), UDP_PORT);

            while (true)
            {
                POINT p;
                GetCursorPos(out p);

                string msg = p.X + "|" + p.Y;
                byte[] data = Encoding.UTF8.GetBytes(msg);

                udp.Send(data, data.Length, ep);

                Thread.Sleep(10); 
            }
        }
        catch
        {
            Console.WriteLine("Connection failed");
        }
    }
}
