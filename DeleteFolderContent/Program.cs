using System.Diagnostics;
using Microsoft.VisualBasic;

internal class Program
{
    private enum ExitCodeResult : int
    {
        Success = 0,
        ShowHelpDoNotRun = 1,
        InvalidArguments = 2
    }

    private record ExecutionParams {
        public uint MinFreeSpaceGb { get; set; }
        public string FolderToClean { get; set; } = "NUL";
        public uint CheckPeriodInMin { get; set; } = 60;
    }

    private static ExecutionParams _executionParams = new();

    private static int Main(string[] args)
    {
        ExitCodeResult exitCode = ValidateArguments(args);

        if (exitCode == ExitCodeResult.Success)
        {
            RunProcess();
        }

        return (int)exitCode;
    }

    private static void RunProcess()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        
        ThreadPool.QueueUserWorkItem(new WaitCallback(MonitorFreeSpace), cancellationTokenSource.Token);
        
        Console.WriteLine("\n>>>>>>>>>> Press any key to end the process... \n");
        Console.ReadKey(true);
        cancellationTokenSource.Cancel();
        Console.WriteLine("Process terminated.");
    }

    private static void MonitorFreeSpace(object? obj)
    {
        var oneGb = (1024*1024*1024);
        var driveInfo = new DriveInfo(Path.GetFullPath(_executionParams.FolderToClean));
        var checkFrequency = new TimeSpan(0, (int)_executionParams.CheckPeriodInMin, 0);
        long freeSpaceGb() => driveInfo.TotalFreeSpace / oneGb;
        void displayCurrentFreeSpace() => Console.WriteLine($"Current free space: {freeSpaceGb()} Gb");

        displayCurrentFreeSpace();

        while (true)
        {
            if (freeSpaceGb() < _executionParams.MinFreeSpaceGb)
            {
                Console.WriteLine($"{DateTime.Now} FreeSpace in {driveInfo.RootDirectory} ({freeSpaceGb()} GB) is below the minimum specified ({_executionParams.MinFreeSpaceGb} GB)");
                Console.WriteLine($"Proceeding to clean the content of: {_executionParams.FolderToClean}");
                DeleteContentInFolder(_executionParams.FolderToClean);
                displayCurrentFreeSpace();
            }
            Console.Title = $"NEXT RUN: {DateTime.Now.AddMinutes(_executionParams.CheckPeriodInMin).ToShortTimeString()}";
            Thread.Sleep(checkFrequency);
        }
    }

    private static void DeleteContentInFolder(string folderToClean)
    {
        var pathToClean = Path.Combine(folderToClean, "*.*");
        Process.Start($"cmd", $"/c del /q /s {pathToClean} 1> NUL 2> NUL").WaitForExit();
    }

    private static ExitCodeResult ValidateArguments(string[] args)
    {
        if (args.Length < 2)
        {
            ShowHelp();
            return ExitCodeResult.ShowHelpDoNotRun;
        }

        if (!uint.TryParse(args[0], out uint minFreeSpace))
        {
            Console.WriteLine($"Invalid value for minimum free space: {args[0]} GB");
            return ExitCodeResult.InvalidArguments;
        }
        _executionParams.MinFreeSpaceGb = minFreeSpace;

        if (!Path.Exists(args[1]))
        {
            Console.WriteLine($"Invalid folder: \"{args[1]}\"");
            return ExitCodeResult.InvalidArguments;
        }
        _executionParams.FolderToClean = args[1];

        if (args.Length > 2)
        {
            if (!uint.TryParse(args[2], out uint checkPeriodInMins))
            {
                Console.WriteLine($"Invalid check period in minutes: {args[2]} min");
                return ExitCodeResult.InvalidArguments;
            }
            _executionParams.CheckPeriodInMin = checkPeriodInMins;
        }

        return ExitCodeResult.Success;
    }

    private static void ShowHelp()
    {
        var programName = Process.GetCurrentProcess().MainModule?.FileName ?? "<program>";
        programName = Path.GetFileName(programName);

        Console.WriteLine("\n| Usage:");
        Console.WriteLine($"|\t{programName} <Min free space (GB)> <Folder to delete content from> [Check period in mins]");
        Console.WriteLine("|\n| Once the minimum space is reached, all the content from the specified folder will be deleted to free up some space.");
        Console.WriteLine("| Minimum space is checked on the same volume of the folder.");
        Console.WriteLine("| Content in the folder that's being used by other processes won't be deleted.");
        Console.WriteLine($"| If [Check period] is not provided, default value is {new ExecutionParams().CheckPeriodInMin} min.");
        Console.WriteLine("|\n| Example: - remove all in C\\Temp once C:\\ is below 50Gb");
        Console.WriteLine($"|\t{programName} 50 C:\\Temp\n");
    }
}
