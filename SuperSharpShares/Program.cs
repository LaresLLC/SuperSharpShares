using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.Protocols;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using DirectoryServicesSearchScope = System.DirectoryServices.SearchScope;
using ProtocolsSearchScope = System.DirectoryServices.Protocols.SearchScope;


namespace SuperSharpShares
{

    public class EnumerateDomainShares
    {
        private const int TimeoutSeconds = 1;
        private const string PrintShareName = "print$";
        private const string AdminShareName = "C$";

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        public static extern int NetShareEnum(
            string serverName,
            int level,
            ref IntPtr bufPtr,
            int prefMaxLen,
            ref int entriesRead,
            ref int totalEntries,
            ref int resumeHandle
        );

        [DllImport("Netapi32.dll")]
        public static extern int NetApiBufferFree(IntPtr buffer);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
    int dwLogonType, int dwLogonProvider, out IntPtr phToken);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public extern static bool CloseHandle(IntPtr handle);

        public static WindowsIdentity Authenticate(string domain, string username, string password)
        {
            const int LOGON32_PROVIDER_DEFAULT = 0;
            const int LOGON32_LOGON_INTERACTIVE = 2;

            IntPtr tokenHandle = IntPtr.Zero;

            try
            {
                
                bool returnValue = LogonUser(username, domain, password,
                    LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT,
                    out tokenHandle);

                if (false == returnValue)
                {
                    int ret = Marshal.GetLastWin32Error();
                    Console.WriteLine("LogonUser failed with error code : {0}", ret);
                    return null;
                }

               
                return new WindowsIdentity(tokenHandle);
            }
            finally
            {
                
                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }
            }
        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHARE_INFO_1
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string shi1_netname;
            public uint shi1_type;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string shi1_remark;
        }
        public static void PrintHelp()
        {
            Console.WriteLine("Usage: SuperSharpShares.exe [options]\n");
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --help                  Show this message and exit.");
            Console.WriteLine("  -auth                       Authenticate with domain credentials.");
            Console.WriteLine("  -domain <domain>            Specify the domain. Example: hacklab.local");
            Console.WriteLine("  -username <username>        Specify the username. Example: username1");
            Console.WriteLine("  -password <password>        Specify the password. Example: Passw0rd1");
        }

        public static int GetSize() => Marshal.SizeOf(typeof(SHARE_INFO_1));

        public static List<string> GetComputerNames(string domainName)
        {
            var computerNames = new List<string>();

            string ldapPath = $"LDAP://{domainName}";

            using (var root = new DirectoryEntry(ldapPath))
            using (var searcher = new DirectorySearcher(root))
            {
                searcher.Filter = "(objectCategory=computer)";
                searcher.SearchScope = DirectoryServicesSearchScope.Subtree;  // Use the alias
                computerNames.AddRange(searcher.FindAll().OfType<SearchResult>().Select(result => result.Properties["name"][0].ToString()));
            }


            return computerNames;
        }

        public static bool CanAccessShare(string sharePath, CancellationToken cancellationToken)
        {
            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
                    Task.Run(() => CheckAccess(sharePath, null), cts.Token).Wait();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void CheckAccess(string sharePath, string computerName)
        {
            var directoryInfo = new DirectoryInfo(sharePath);

            bool canRead = directoryInfo.Exists;

            if (canRead)
            {
                try
                {
                    string testFilePath = Path.Combine(sharePath, Guid.NewGuid().ToString("N") + ".txt");
                    File.WriteAllText(testFilePath, "Test");
                    File.Delete(testFilePath);

                    Console.WriteLine($"{computerName}{sharePath} - Read and Write access");
                }
                catch
                {
                    Console.WriteLine($"{computerName}{sharePath} - Read access");
                }
            }
        }

        private static void PrintResults(List<string> results)
        {
            var filteredResults = results.Where(r => r.Contains("Read access") || r.Contains("Read and Write access")).ToList();

            for (int i = 0; i < filteredResults.Count; i++)
            {
                Console.WriteLine(filteredResults[i]);

                if (i < filteredResults.Count - 1)
                {
                    Console.WriteLine();
                }
            }
        }

        public static void EnumerateDomainAndShares(string domainName, CancellationToken cancellationToken)
        {
            List<string> computerNames = GetComputerNames(domainName);

            int numberOfThreads = Environment.ProcessorCount;

            Console.WriteLine($"Number of threads: {numberOfThreads}");
            Console.WriteLine();

            var allResults = new List<string>();

            var tasks = computerNames.Select(computerName => Task.Factory.StartNew(() =>
            {
                var accessibleShares = new List<string>();
                var scannedShares = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                IntPtr ptrInfo = IntPtr.Zero;
                int entriesRead = 0;
                int resumeHandle = 0;

                try
                {
                    using (var ctsTask = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        int result = NetShareEnum(computerName, 1, ref ptrInfo, -1, ref entriesRead, ref entriesRead, ref resumeHandle);

                        if (result == 0 && ptrInfo.ToInt64() > 0)
                        {
                            int increment = GetSize();

                            Parallel.For(0, entriesRead, i =>
                            {
                                var newIntPtr = new IntPtr(ptrInfo.ToInt64() + i * increment);
                                var info = Marshal.PtrToStructure<SHARE_INFO_1>(newIntPtr);

                                string shareName = info.shi1_netname;

                                if (shareName.Equals(PrintShareName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return;
                                }

                                string sharePath = $"\\\\{computerName}\\{shareName}";
                                bool isAdminShare = shareName.Equals(AdminShareName, StringComparison.OrdinalIgnoreCase);

                                if (CanAccessShare(sharePath, ctsTask.Token))
                                {

                                    bool hasReadWriteAccess = sharePath.EndsWith(" - Read and Write access");

                                    if (isAdminShare && hasReadWriteAccess)
                                    {
                                        shareName += " - Admin";
                                    }

                                    lock (accessibleShares)
                                    {
                                        accessibleShares.Add($"\\\\{computerName}\\{shareName}");
                                    }
                                }
                            });

                            NetApiBufferFree(ptrInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error on {computerName}: {ex.Message}");
                }

                lock (allResults)
                {
                    allResults.AddRange(accessibleShares);
                }

                PrintResults(accessibleShares);

                return accessibleShares;
            }, cancellationToken)).ToArray();

            Task.WaitAll(tasks);

            PrintResults(allResults);
        }

        public static void Main(string[] args)
        {
            string domain = "", username = "", password = "";
            string currentusr = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            bool requireAuth = args.Contains("-auth");
            int domainIndex = Array.IndexOf(args, "-domain");
            int userIndex = Array.IndexOf(args, "-username");
            int passIndex = Array.IndexOf(args, "-password");
            int helpIndex = Array.IndexOf(args, "-h") != -1 ? Array.IndexOf(args, "-h") : Array.IndexOf(args, "--help");

            if (helpIndex != -1)
            {
                PrintHelp();
                return;
            }

            WindowsIdentity identity = null;

            if (domainIndex != -1 && domainIndex < args.Length - 1)
            {
                domain = args[domainIndex + 1];
            }
            else
            {
                domain = Environment.GetEnvironmentVariable("USERDOMAIN");
            }

            if (requireAuth)
            {
                if (userIndex != -1 && passIndex != -1 && userIndex < args.Length - 1 && passIndex < args.Length - 1)
                {
                    username = args[userIndex + 1];
                    password = args[passIndex + 1];
                    identity = AuthenticationHelper.Authenticate(domain, username, password);
                    if (identity == null)
                    {
                        Console.WriteLine("Authentication failed. Exiting application.");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("[!] Authentication required but not all parameters were provided. Usage: -auth -domain <domain> -username <username> -password <password>");
                    return;
                }
            }
            else
            {
                Console.WriteLine($"[!] Executing with current user: {currentusr} token against {domain}, no credentials supplied");
            }

            using (var cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, e) => { e.Cancel = true; cts.Cancel(); };

                if (identity != null)
                {
                    using (identity.Impersonate())
                    {
                        EnumerateDomainAndShares(domain, cts.Token);
                    }
                    identity.Dispose();  
                }
                else
                {
                    EnumerateDomainAndShares(domain, cts.Token);
                }

            }
        }

        public static class AuthenticationHelper
        {
            public static WindowsIdentity Authenticate(string domain, string username, string password)
            {
                if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    Console.WriteLine("Missing arguments. Usage: Provide domain, username, and password.");
                    return null;
                }

                using (PrincipalContext context = new PrincipalContext(ContextType.Domain, domain))
                {
                    if (context.ValidateCredentials(username, password))
                    {
                        Console.WriteLine($"Successfully authenticated to {domain} as {username}");
                        return new WindowsIdentity(username + "@" + domain);
                    }
                    else
                    {
                        Console.WriteLine("Invalid credentials");
                        return null;
                    }
                }
            }
        }

    }
}