using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SuperSharpShares { 
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHARE_INFO_1
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string shi1_netname;
        public uint shi1_type;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string shi1_remark;
    }

    public static int GetSize() => Marshal.SizeOf(typeof(SHARE_INFO_1));

    public static List<string> GetComputerNames(string domainName)
    {
        var computerNames = new List<string>();
        string ldapPath = $"LDAP://{domainName}";

        using (var root = new DirectoryEntry(ldapPath))
        using (var searcher = new DirectorySearcher(root, "(objectCategory=computer)"))
        {
            searcher.SearchScope = SearchScope.Subtree;
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

    public static void EnumerateDomainAndShares(CancellationToken cancellationToken)
    {
        string domainName = Environment.GetEnvironmentVariable("USERDOMAIN");
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
        using (var cts = new CancellationTokenSource())
        {
            Console.CancelKeyPress += (sender, args) => { args.Cancel = true; cts.Cancel(); };
            EnumerateDomainAndShares(cts.Token);
        }
    }
}
}