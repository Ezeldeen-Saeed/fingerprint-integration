using System;
using System.Runtime.InteropServices;

namespace ZkConnectionTest;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== ZKTeco Connection Test ===");
        Console.WriteLine();

        string ip = "192.168.1.11";
        int port = 8089;

        Console.WriteLine($"Testing connection to {ip}:{port}");
        Console.WriteLine();

        dynamic? zkem = null;
        try
        {
            // Step 1: Get COM type
            Console.WriteLine("Step 1: Getting COM type...");
            var zkType = Type.GetTypeFromProgID("zkemkeeper.ZKEM");
            if (zkType == null)
            {
                Console.WriteLine("❌ FAILED: zkemkeeper.ZKEM not registered!");
                Console.WriteLine("   Run the SDK installer first.");
                return;
            }
            Console.WriteLine("✓ COM type found");

            // Step 2: Create instance
            Console.WriteLine("Step 2: Creating instance...");
            zkem = Activator.CreateInstance(zkType);
            if (zkem == null)
            {
                Console.WriteLine("❌ FAILED: Could not create instance!");
                return;
            }
            Console.WriteLine("✓ Instance created");

            // Step 3: Set timeout
            Console.WriteLine("Step 3: Setting timeout to 5000ms...");
            zkem.SetCommTimeout(5000);
            Console.WriteLine("✓ Timeout set");

            // Step 4: Try to connect
            Console.WriteLine($"Step 4: Calling Connect_Net({ip}, {port})...");
            Console.WriteLine("   (This may take a few seconds...)");
            
            bool connected = zkem.Connect_Net(ip, port);

            if (connected)
            {
                Console.WriteLine();
                Console.WriteLine("✅ SUCCESS! Connected to device!");
                Console.WriteLine();

                // Try to get device info
                try
                {
                    string sn = "";
                    if (zkem.GetSerialNumber(1, out sn))
                    {
                        Console.WriteLine($"   Serial Number: {sn}");
                    }

                    string deviceName = "";
                    if (zkem.GetDeviceInfo(1, 72, ref deviceName))
                    {
                        Console.WriteLine($"   Device Name: {deviceName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   Could not get device info: {ex.Message}");
                }

                zkem.Disconnect();
                Console.WriteLine();
                Console.WriteLine("Disconnected successfully.");
            }
            else
            {
                int errorCode = 0;
                zkem.GetLastError(ref errorCode);
                
                Console.WriteLine();
                Console.WriteLine($"❌ FAILED: Connect_Net returned false");
                Console.WriteLine($"   Error Code: {errorCode}");
                Console.WriteLine();
                Console.WriteLine("Error Code Meanings:");
                Console.WriteLine("  -7  = Connection timeout/failed");
                Console.WriteLine("  -2  = Invalid parameters");
                Console.WriteLine("  -5  = Data error");
                Console.WriteLine();
                Console.WriteLine("Possible Causes:");
                Console.WriteLine("  1. Wrong IP address");
                Console.WriteLine("  2. Wrong Port (Web port vs SDK port)");
                Console.WriteLine("  3. Device has Communication Password set");
                Console.WriteLine("  4. Cloud Mode is enabled (blocks SDK)");
                Console.WriteLine("  5. PC Connection is disabled on device");
                Console.WriteLine("  6. Firewall blocking connection");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"❌ EXCEPTION: {ex.Message}");
            Console.WriteLine($"   Type: {ex.GetType().Name}");
            Console.WriteLine();
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            if (zkem != null && Marshal.IsComObject(zkem))
            {
                Marshal.FinalReleaseComObject(zkem);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
