using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mass_Process_Memory_Dumper {
    internal class Program {
        static Random r = new Random();

        static Stopwatch sw = new Stopwatch();

        static List<string> svchostList = new List<string>();

        static string[] exclusionList = { "svchost", "cmd", "opera", "firefox", "Discord" }; // always keep svchost in this list, this prevents duplicate dumps

        static string additionalCommands = ""; // add your own commands

        static string path = AppDomain.CurrentDomain.BaseDirectory;

        static void Main(string[] args) {
            Console.CursorVisible = false;

            Console.WindowWidth = 80;
            Console.BufferWidth = 80;

            Console.WindowHeight = 20;
            Console.BufferHeight = 20;

            Console.WriteLine("Mass Process Memory Dumper - github.com/thatnword");

            // setup all needed directories and files
            if (!Directory.Exists("dumps") || !Directory.Exists("assets")) {
                Directory.CreateDirectory("dumps");
                Directory.CreateDirectory("assets");
                File.WriteAllBytes("assets\\dumper.exe", Properties.Resources.s2);
            }

            runCommand($"cd {path}assets & tasklist /svc | find \"svchost.exe\" > svchost.log");

            Thread.Sleep(500);

            sw.Start();

            // gather service list and dump them
            getSvchost();
            dumpSvchost();

            // dump all normal processes excluding whatever is in the Exclusion List
            dumpProcesses();

            // wait for all dumps to be fully finished
            int currentProcCount = Process.GetProcessesByName("dumper").Count();

            while (currentProcCount > 0) {
                currentProcCount = Process.GetProcessesByName("dumper").Count();
                Thread.Sleep(5);
            }

            Console.WriteLine($"\n[#] Step 3\n -  Finished dumping all processes in {sw.ElapsedMilliseconds}ms");
            Console.ReadLine();
        }


        /// <summary>
        /// Dumping normal running processes
        /// Multithreaded for speed with customizable commands & process exclusions
        /// </summary>
        static void dumpProcesses() {
            Console.WriteLine("\n[#] Step 2\n -  Dumping processes");

            var allProcesses = Process.GetProcesses();
            foreach (Process p in allProcesses) {
                new Thread(() => {
                    try {
                        if (!exclusionList.Contains(p.ProcessName)) {
                            // creat directory for specific process if it doesnt exist
                            if (!Directory.Exists($"dumps\\{p.ProcessName}"))
                                Directory.CreateDirectory($"dumps\\{p.ProcessName}");

                            // dump process
                            runCommand($"\"{path}assets\\\"dumper.exe -pid {p.Id} -l 4 -nh {additionalCommands} > \"{path}dumps\\{p.ProcessName}\\\"{p.ProcessName}_{r.Next(0, 999999999)}.txt");
                        }
                    } catch { Console.WriteLine($"Failed to dump process \"{p.ProcessName}\""); }
                }).Start();
            }

            Thread.Sleep(5000);
        }

        /// <summary>
        /// Windows servivce dumper
        /// Multithreaded for speed with customizable commands
        /// </summary>
        static void dumpSvchost() {
            Console.WriteLine("\n[#] Step 1\n -  Dumping Windows Services");

            // creat special directory for all services to go
            if (!Directory.Exists("dumps\\svchost"))
                Directory.CreateDirectory("dumps\\svchost");

            foreach (string service in svchostList) {
                new Thread(() => {
                    try {
                        // creat directory for specific service if it doesnt exist
                        if (!Directory.Exists($"dumps\\svchost\\{service}"))
                            Directory.CreateDirectory($"dumps\\svchost\\{service}");

                        // dump service 
                        runCommand($"\"{path}assets\\\"dumper.exe -pid {getService(service)} -l 4 -nh {additionalCommands} > \"{path}dumps\\svchost\\{service}\\\"{service}_{r.Next(0, 999999999)}.txt");
                    } catch { Console.WriteLine($"Failed to dump service \"{service}\""); }
                }).Start();
            }
        }

        /// <summary>
        /// Parse through svchost list and grab only the service name
        /// This is to allow for detailed file logs with exact service name
        /// </summary>
        static void getSvchost() {
            string reader = File.ReadAllText("assets\\svchost.log");
            foreach (string line in reader.Split('\n')) {
                if (line.Length > 5) {
                    string serviceName = line.Substring(35).Replace(" ", "").Replace(",", ".");
                    svchostList.Add(serviceName.Substring(0, serviceName.Length - 1));
                }
            }
        }

        /// <summary>
        /// Gets the PID of specifc service via the Service Name
        /// </summary>
        static uint getService(string serviceName) {
            uint processId = 0;
            string qry = "SELECT PROCESSID FROM WIN32_SERVICE WHERE NAME = '" + serviceName + "'";
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(qry);

            foreach (System.Management.ManagementObject mngntObj in searcher.Get())
                processId = (uint)mngntObj["PROCESSID"];

            return processId;
        }

        /// <summary>
        /// Run any command input through cmd as administrator
        /// </summary>
        static void runCommand(string command) {
            Process CMD = new Process();
            CMD.StartInfo.FileName = "cmd.exe";
            CMD.StartInfo.RedirectStandardInput = true;
            CMD.StartInfo.RedirectStandardOutput = true;
            CMD.StartInfo.CreateNoWindow = true;
            CMD.StartInfo.UseShellExecute = false;
            CMD.Start();

            CMD.StandardInput.WriteLine(command);
            CMD.StandardInput.Flush();
            CMD.StandardInput.Close();
        }

        static string getParent(int pid) {
            try {
                var myId = pid;
                var query = string.Format("SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {0}", myId);
                var search = new ManagementObjectSearcher("root\\CIMV2", query);
                var results = search.Get().GetEnumerator();
                results.MoveNext();
                var queryObj = results.Current;
                var parentId = (uint)queryObj["ParentProcessId"];
                var parent = Process.GetProcessById((int)parentId);

                return parent.ProcessName;
            } catch (Exception e) {
                return "prolly died so is fine";
            }
        }
    }
}