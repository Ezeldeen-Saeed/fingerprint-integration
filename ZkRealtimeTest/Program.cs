using System;
using System.Runtime.InteropServices;

namespace ZkRealtimeTest;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== ZKTeco Real-Time Events Test ===");
        Console.WriteLine();

        string ip = "192.168.1.10";
        int port = 4370;

        dynamic? zkem = null;
        try
        {
            var zkType = Type.GetTypeFromProgID("zkemkeeper.ZKEM");
            if (zkType == null)
            {
                Console.WriteLine("❌ SDK not registered!");
                return;
            }

            zkem = Activator.CreateInstance(zkType);
            
            Console.WriteLine($"Connecting to {ip}:{port}...");
            bool connected = zkem.Connect_Net(ip, port);

            if (!connected)
            {
                int errorCode = 0;
                zkem.GetLastError(ref errorCode);
                Console.WriteLine($"❌ Connection failed (error {errorCode})");
                return;
            }

            Console.WriteLine("✓ Connected!");
            Console.WriteLine();

            // Try to register for real-time events
            Console.WriteLine("Testing real-time event support...");
            
            try
            {
                // RegEvent registers for real-time notifications
                // Parameters: MachineNumber, EventMask
                // EventMask: 65535 = all events
                bool regResult = zkem.RegEvent(1, 65535);
                
                if (regResult)
                {
                    Console.WriteLine("✅ Real-time events ARE SUPPORTED!");
                    Console.WriteLine();
                    Console.WriteLine("Your device can send real-time notifications.");
                    Console.WriteLine("This means we can implement instant sync when someone scans!");
                    Console.WriteLine();
                    Console.WriteLine("Listening for events for 30 seconds...");
                    Console.WriteLine("(Try scanning a fingerprint on the device)");
                    Console.WriteLine();

                    // Wait for events
                    System.Threading.Thread.Sleep(30000);
                }
                else
                {
                    Console.WriteLine("❌ Real-time events NOT supported");
                    Console.WriteLine("Device does not support RegEvent.");
                    Console.WriteLine("You must use polling (timer-based sync).");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Real-time events NOT supported");
                Console.WriteLine($"   Error: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Your device does not support real-time events.");
                Console.WriteLine("You must use polling (timer-based sync).");
            }

            zkem.Disconnect();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
        finally
        {
            if (zkem != null && Marshal.IsComObject(zkem))
            {
                Marshal.FinalReleaseComObject(zkem);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Test complete.");
    }
}
