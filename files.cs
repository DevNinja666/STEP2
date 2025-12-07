using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using System.Security.Cryptography;

namespace MiniOrderApp
{
    public class Service
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
    }

    public class Order
    {
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public List<Service> Services { get; set; } = new List<Service>();
        public decimal Total => ComputeTotal();
        private decimal ComputeTotal()
        {
            decimal s = 0;
            foreach (var svc in Services) s += svc.Price;
            return s;
        }
    }

    public class UserData
    {
        public string Username { get; set; }
        public string Password { get; set; } // stored as plain/text or hashed (string)
        public List<Order> Orders { get; set; } = new List<Order>();
    }

    class Program
    {
        static readonly string BaseDataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");

        static void Main()
        {
            Directory.CreateDirectory(BaseDataDir);
            Console.WriteLine("=== Mini Order Service ===");
            while (true)
            {
                Console.WriteLine("\n1) Register\n2) Login\n3) Exit");
                Console.Write("Choose: ");
                var k = Console.ReadLine()?.Trim();
                if (k == "1") Register();
                else if (k == "2")
                {
                    var user = Login();
                    if (user != null) UserMenu(user);
                }
                else if (k == "3" || string.Equals(k, "exit", StringComparison.OrdinalIgnoreCase)) break;
            }
        }

        static void Register()
        {
            Console.Write("Username: ");
            var username = (Console.ReadLine() ?? "").Trim();
            if (string.IsNullOrEmpty(username))
            {
                Console.WriteLine("Username cannot be empty.");
                return;
            }

            string userDir = Path.Combine(BaseDataDir, username);
            if (Directory.Exists(userDir))
            {
                Console.WriteLine("User already exists.");
                return;
            }

            Console.Write("Password: ");
            var pwd = ReadPassword();
            if (string.IsNullOrEmpty(pwd))
            {
                Console.WriteLine("Password cannot be empty.");
                return;
            }

            Directory.CreateDirectory(userDir);

            var userData = new UserData { Username = username, Password = pwd };
            SaveUserMeta(userData, userDir);

            WriteLog(username, "Registered");
            Console.WriteLine("Registered successfully.");
        }

        static UserData Login()
        {
            Console.Write("Username: ");
            var username = (Console.ReadLine() ?? "").Trim();
            Console.Write("Password: ");
            var pwd = ReadPassword();

            string userDir = Path.Combine(BaseDataDir, username);
            if (!Directory.Exists(userDir))
            {
                Console.WriteLine("User not found.");
                return null;
            }

            var meta = LoadUserMeta(userDir);
            if (meta == null)
            {
                Console.WriteLine("User data corrupted.");
                return null;
            }

            if (meta.Password != pwd)
            {
                Console.WriteLine("Wrong password.");
                return null;
            }

            Console.WriteLine("Login successful.");
            WriteLog(username, "Login");

          
            var ordersLoaded = false;
            try
            {
                string jsonPath = Path.Combine(userDir, "orders.json");
                string xmlPath = Path.Combine(userDir, "orders.xml");
                string binPath = Path.Combine(userDir, "orders.dat");
                if (File.Exists(jsonPath))
                {
                    meta.Orders = LoadOrdersJson(jsonPath) ?? new List<Order>();
                    ordersLoaded = true;
                    WriteLog(username, "Auto-loaded orders.json");
                }
                else if (File.Exists(xmlPath))
                {
                    meta.Orders = LoadOrdersXml(xmlPath) ?? new List<Order>();
                    ordersLoaded = true;
                    WriteLog(username, "Auto-loaded orders.xml");
                }
                else if (File.Exists(binPath))
                {
                    meta.Orders = LoadOrdersBinary(binPath) ?? new List<Order>();
                    ordersLoaded = true;
                    WriteLog(username, "Auto-loaded orders.dat");
                }
            }
            catch
            {
                Console.WriteLine("Auto-load failed (file may be corrupted).");
            }

            if (ordersLoaded)
                Console.WriteLine($"Loaded {meta.Orders.Count} orders for {username}.");

            return meta;
        }

        static void UserMenu(UserData user)
        {
            while (true)
            {
                Console.WriteLine($"\n--- {user.Username} ---");
                Console.WriteLine("1) View orders");
                Console.WriteLine("2) Add order");
                Console.WriteLine("3) Save data");
                Console.WriteLine("4) Load data");
                Console.WriteLine("5) Show total sum of orders");
                Console.WriteLine("6) Logout");
                Console.Write("Choose: ");
                var cmd = Console.ReadLine()?.Trim();
                if (cmd == "1") ViewOrders(user);
                else if (cmd == "2") AddOrder(user);
                else if (cmd == "3") SaveDataMenu(user);
                else if (cmd == "4") LoadDataMenu(user);
                else if (cmd == "5") ShowTotal(user);
                else if (cmd == "6") { WriteLog(user.Username, "Logout"); break; }
            }
        }

        static void ViewOrders(UserData user)
        {
            if (user.Orders.Count == 0) { Console.WriteLine("No orders."); return; }
            for (int i = 0; i < user.Orders.Count; i++)
            {
                var o = user.Orders[i];
                Console.WriteLine($"#{i + 1} {o.Title} | {o.Date.ToString("s", CultureInfo.InvariantCulture)} | Total: {o.Total:C}");
                for (int j = 0; j < o.Services.Count; j++)
                    Console.WriteLine($"    - {o.Services[j].Name} : {o.Services[j].Price:C}");
            }
        }

        static void AddOrder(UserData user)
        {
            Console.Write("Order title: ");
            var title = (Console.ReadLine() ?? "").Trim();
            if (string.IsNullOrEmpty(title)) { Console.WriteLine("Title empty."); return; }
            var order = new Order { Title = title, Date = DateTime.Now };

            while (true)
            {
                Console.Write("Service name (empty to finish): ");
                var sname = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(sname)) break;
                Console.Write("Price: ");
                var sp = Console.ReadLine();
                if (!decimal.TryParse(sp, out decimal price) || price < 0) { Console.WriteLine("Invalid price."); continue; }
                order.Services.Add(new Service { Name = sname.Trim(), Price = price });
            }

            user.Orders.Add(order);
            Console.WriteLine("Order added.");
            WriteLog(user.Username, $"Added order '{order.Title}'");
            // save meta (username/password) to ensure user file exists
            SaveUserMeta(user, Path.Combine(BaseDataDir, user.Username));
        }

        static void SaveDataMenu(UserData user)
        {
            string userDir = Path.Combine(BaseDataDir, user.Username);
            Directory.CreateDirectory(userDir);

            Console.WriteLine("Save as: 1) JSON  2) XML  3) Binary  4) All three");
            Console.Write("Choose: ");
            var choice = Console.ReadLine()?.Trim();

            if (choice == "1" || choice == "4")
            {
                var jsonPath = Path.Combine(userDir, "orders.json");
                try
                {
                    SaveOrdersJson(user.Orders, jsonPath);
                    Console.WriteLine($"Saved JSON: {jsonPath}");
                    WriteLog(user.Username, "Saved orders.json");
                }
                catch (Exception ex) { Console.WriteLine("JSON save error: " + ex.Message); }
            }
            if (choice == "2" || choice == "4")
            {
                var xmlPath = Path.Combine(userDir, "orders.xml");
                try
                {
                    SaveOrdersXml(user.Orders, xmlPath);
                    Console.WriteLine($"Saved XML: {xmlPath}");
                    WriteLog(user.Username, "Saved orders.xml");
                }
                catch (Exception ex) { Console.WriteLine("XML save error: " + ex.Message); }
            }
            if (choice == "3" || choice == "4")
            {
                var binPath = Path.Combine(userDir, "orders.dat");
                try
                {
                    SaveOrdersBinary(user.Orders, binPath);
                    Console.WriteLine($"Saved Binary: {binPath}");
                    WriteLog(user.Username, "Saved orders.dat");
                }
                catch (Exception ex) { Console.WriteLine("Binary save error: " + ex.Message); }
            }

            // compare sizes if all three exist
            var pjs = Path.Combine(userDir, "orders.json");
            var pxm = Path.Combine(userDir, "orders.xml");
            var pdb = Path.Combine(userDir, "orders.dat");
            if (File.Exists(pjs) && File.Exists(pxm) && File.Exists(pdb))
            {
                var s1 = new FileInfo(pjs).Length;
                var s2 = new FileInfo(pxm).Length;
                var s3 = new FileInfo(pdb).Length;
                Console.WriteLine($"Sizes: JSON={s1} bytes, XML={s2} bytes, BIN={s3} bytes");
            }
        }

        static void LoadDataMenu(UserData user)
        {
            string userDir = Path.Combine(BaseDataDir, user.Username);
            Console.WriteLine("Load from: 1) JSON  2) XML  3) Binary");
            Console.Write("Choose: ");
            var c = Console.ReadLine()?.Trim();
            try
            {
                if (c == "1")
                {
                    var p = Path.Combine(userDir, "orders.json");
                    var list = LoadOrdersJson(p);
                    if (list != null) { user.Orders = list; Console.WriteLine("Loaded from JSON."); WriteLog(user.Username, "Loaded orders.json"); }
                }
                else if (c == "2")
                {
                    var p = Path.Combine(userDir, "orders.xml");
                    var list = LoadOrdersXml(p);
                    if (list != null) { user.Orders = list; Console.WriteLine("Loaded from XML."); WriteLog(user.Username, "Loaded orders.xml"); }
                }
                else if (c == "3")
                {
                    var p = Path.Combine(userDir, "orders.dat");
                    var list = LoadOrdersBinary(p);
                    if (list != null) { user.Orders = list; Console.WriteLine("Loaded from Binary."); WriteLog(user.Username, "Loaded orders.dat"); }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Load error: " + ex.Message);
            }
        }

        static void ShowTotal(UserData user)
        {
            decimal total = 0;
            foreach (var o in user.Orders) total += o.Total;
            Console.WriteLine($"Total sum of all orders: {total:C}");
        }

        static string ReadPassword()
        {
            var sb = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
                if (key.Key == ConsoleKey.Backspace && sb.Length > 0) { sb.Length--; Console.Write("\b \b"); }
                else if (!char.IsControl(key.KeyChar)) { sb.Append(key.KeyChar); Console.Write("*"); }
            }
            return sb.ToString();
        }

        static void SaveUserMeta(UserData user, string userDir)
        {
            try
            {
                var metaPath = Path.Combine(userDir, "user.json");
                var opt = new JsonSerializerOptions { WriteIndented = true };
                var meta = new UserData { Username = user.Username, Password = user.Password, Orders = user.Orders ?? new List<Order>() };
                File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, opt));
            }
            catch { }
        }

        static UserData LoadUserMeta(string userDir)
        {
            try
            {
                var metaPath = Path.Combine(userDir, "user.json");
                if (!File.Exists(metaPath)) return null;
                var text = File.ReadAllText(metaPath);
                return JsonSerializer.Deserialize<UserData>(text);
            }
            catch { return null; }
        }

        static void SaveOrdersJson(List<Order> orders, string path)
        {
            var opt = new JsonSerializerOptions { WriteIndented = true };
            var text = JsonSerializer.Serialize(orders, opt);
            File.WriteAllText(path, text);
        }

        static List<Order> LoadOrdersJson(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var text = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<Order>>(text);
            }
            catch
            {
                Console.WriteLine("JSON file corrupted or invalid.");
                return null;
            }
        }

        static void SaveOrdersXml(List<Order> orders, string path)
        {
            var xs = new XmlSerializer(typeof(List<Order>));
            using (var fs = new FileStream(path, FileMode.Create))
                xs.Serialize(fs, orders);
        }

        static List<Order> LoadOrdersXml(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var xs = new XmlSerializer(typeof(List<Order>));
                using (var fs = new FileStream(path, FileMode.Open))
                    return (List<Order>)xs.Deserialize(fs);
            }
            catch
            {
                Console.WriteLine("XML file corrupted or invalid.");
                return null;
            }
        }

        static void SaveOrdersBinary(List<Order> orders, string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs, Encoding.UTF8))
            {
                bw.Write(orders.Count);
                foreach (var o in orders)
                {
                    bw.Write(o.Title ?? "");
                    bw.Write(o.Date.ToBinary());
                    bw.Write(o.Services?.Count ?? 0);
                    if (o.Services != null)
                    {
                        foreach (var s in o.Services)
                        {
                            bw.Write(s.Name ?? "");
                            bw.Write((double)s.Price);
                        }
                    }
                }
            }
        }

        static List<Order> LoadOrdersBinary(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var list = new List<Order>();
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs, Encoding.UTF8))
                {
                    int ordersCount = br.ReadInt32();
                    for (int i = 0; i < ordersCount; i++)
                    {
                        var title = br.ReadString();
                        var date = DateTime.FromBinary(br.ReadInt64());
                        int svcCount = br.ReadInt32();
                        var order = new Order { Title = title, Date = date };
                        for (int j = 0; j < svcCount; j++)
                        {
                            var sname = br.ReadString();
                            var price = (decimal)br.ReadDouble();
                            order.Services.Add(new Service { Name = sname, Price = price });
                        }
                        list.Add(order);
                    }
                }
                return list;
            }
            catch
            {
                Console.WriteLine("Binary file corrupted or invalid.");
                return null;
            }
        }

        static void WriteLog(string username, string action)
        {
            try
            {
                string userDir = Path.Combine(BaseDataDir, username);
                Directory.CreateDirectory(userDir);
                var logPath = Path.Combine(userDir, "log.txt");
                File.AppendAllText(logPath, $"{DateTime.Now:s} - {action}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
