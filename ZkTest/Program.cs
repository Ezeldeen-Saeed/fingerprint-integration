using System;
using System.Runtime.InteropServices;

namespace ZkTest;

class Program
{
    static void Main(string[] args)
    {
        string ip = "192.168.1.11";
        int port = 4370;
        int machineNumber = 1;
        int commPassword = 0; // Try 0 as default

        Console.WriteLine($"Testing connection to {ip}:{port}...");
        Console.WriteLine($"Machine Number: {machineNumber}");
        Console.WriteLine($"Comm Password: {commPassword}");

        try
        {
            var zkType = Type.GetTypeFromProgID("zkemkeeper.ZKEM");
            if (zkType == null)
            {
                Console.WriteLine("❌ SDK not registered! Run the SDK installer as Admin.");
                return;
            }

            dynamic zkem = Activator.CreateInstance(zkType);
            
            // Set comm password (important if device has one, or sometimes even if 0)
            zkem.SetCommPassword(commPassword);

            // Try connecting
            Console.WriteLine("Connecting...");
            bool connected = zkem.Connect_Net(ip, port);
            
            if (connected)
            {
                Console.WriteLine("✅ Connected successfully!");
                
                string sn = "";
                zkem.GetSerialNumber(machineNumber, out sn);
                Console.WriteLine($"   Serial Number: {sn}");
                
                string deviceName = "";
                zkem.GetDeviceInfo(machineNumber, 72, ref deviceName);
                Console.WriteLine($"   Device Name: {deviceName}");

                zkem.Disconnect();
            }
            else
            {
                int errorCode = 0;
                zkem.GetLastError(ref errorCode);
                Console.WriteLine($"❌ Connection failed. Error code: {errorCode}");
                
                if (errorCode == -7) Console.WriteLine("   Possible causes:");
                if (errorCode == -7) Console.WriteLine("   1. Wrong Port (Double check device menu -> Network)");
                if (errorCode == -7) Console.WriteLine("   2. Wrong Comm Password (check device menu -> Comm)");
                if (errorCode == -7) Console.WriteLine("   3. Firewall blocking return traffic");
                if (errorCode == -7) Console.WriteLine("   4. Device is busy or not responding to SDK packets");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception: {ex.Message}");
        }
    }
}
