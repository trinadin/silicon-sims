using FSO.Client;
using FSO.Common;
using FSO.LotView;
using Simitone.Client;
using Simitone.Windows.GameLocator;
using Simitone.Windows.UI;
using Simitone.Windows.Utils;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Eto.Forms;

namespace Simitone.Windows
{
    /// <summary>
    /// Cross-platform entry point for Simitone (Linux/macOS/Windows with OpenGL)
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Directory.SetCurrentDirectory(baseDir);
            
            OperatingSystem os = Environment.OSVersion;
            PlatformID pid = os.Platform;
            bool linux = pid == PlatformID.MacOSX || pid == PlatformID.Unix;
            
            if (linux && Directory.Exists("/Users"))
                MonogameLinker.AssemblyDir = "Monogame/MacOS/";
            else if (linux)
                MonogameLinker.AssemblyDir = "Monogame/Linux/";
            else
                MonogameLinker.AssemblyDir = "./";
            
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            string userDir;
            var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            // 2026-10-06 debrand: the user data folder is "The Sims" now — an
            // existing "Simitone" folder from earlier builds keeps being used
            // (saves/config continuity); fresh installs get the new name.
            string BrandDir(string parent) =>
                Directory.Exists(Path.Combine(parent, "Simitone")) ? "Simitone" : "The Sims";
            if (!string.IsNullOrEmpty(myDocs))
            {
                userDir = Path.Combine(myDocs, BrandDir(myDocs) + "/");
            }
            else
            {
                // fallback for Linux: use ~/.local/share/<brand> or ~/<brand>
                var localShare = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localShare))
                {
                    userDir = Path.Combine(localShare, BrandDir(localShare) + "/");
                }
                else
                {
                    // fallback to home if nothing else
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    userDir = Path.Combine(home, BrandDir(home) + "/");
                }
            }
            // Explicit test-only isolation must precede GlobalSettings.Default.
            // Separate checkouts otherwise still share Documents/Simitone/config.ini.
            int testDirIndex = Array.IndexOf(args, "-autotest-userdir");
            if (testDirIndex >= 0)
            {
                if (!args.Contains("-autotest") || testDirIndex + 1 >= args.Length
                    || args[testDirIndex + 1].StartsWith("-"))
                    throw new ArgumentException("-autotest-userdir requires -autotest and a directory.");
                userDir = Path.GetFullPath(args[testDirIndex + 1]).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            }
            userDir = userDir.Replace('\\', '/');
            FSOEnvironment.UserDir = userDir;
            // Byte-level diagnostics to /tmp (userDir can resolve differently between launch
            // modes; /tmp is always readable from the harness).
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "thesims_diag.log"),
                DateTime.Now.ToString("HH:mm:ss.fff") + " boot args=[" + string.Join(" | ", args) + "] userdir=" + userDir + Environment.NewLine); } catch {}
            try
            {
                Directory.CreateDirectory(FSOEnvironment.UserDir);
            }
            catch
            {
                if (testDirIndex >= 0) throw;
                // macOS sandbox / read-only Documents: fall back to the application bundle directory.
                Console.WriteLine("Warning: could not create user data directory '" + userDir + "'. Using the application directory instead.");
                userDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "The Sims/").Replace('\\', '/');
                Directory.CreateDirectory(userDir);
                FSOEnvironment.UserDir = userDir;
            }

            ILocator gameLocator;
            if (linux && Directory.Exists("/Users"))
                gameLocator = new MacOSLocator();
            else if (linux)
                gameLocator = new LinuxLocator();
            else
                gameLocator = new WindowsLocator();

            // default to OpenGL on all platforms for the Desktop build
            var useDX = false;
            
            // path priority: 1. Command line (set later), 2. config.ini, 3. Auto-detect
            string? path = null;
            bool pathProvidedViaCommandLine = false;
            var configPath = GlobalSettings.Default.TS1HybridPath;
            if (!string.IsNullOrEmpty(configPath) && configPath != "D:/Games/The Sims/" && 
                File.Exists(Path.Combine(configPath, "GameData", "Behavior.iff")))
            {
                path = configPath;
            }

            FSOEnvironment.Enable3D = false;
            bool ide = false;
            bool aa = false;
            bool jit = false;
            
            #region User resolution parameters

            FSOEnvironment.Args = string.Join(" ", args);

            // AUTOTEST pre-scan (-autotest[\"H1,H2\"] -autotest-opts terms -autotest-timeout ms -autotest-noexit)
            bool autotestEnabled = false;
            string autotestHouses = null;
            string autotestChecks = null;
            int autotestTimeout = 0;
            bool autotestNoExit = false;
            bool textdiagEnabled = false;
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a == "-autotest")
                {
                    autotestEnabled = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-")) autotestHouses = args[++i];
                }
                else if (a == "-autotest-opts") autotestChecks = args[++i];
                else if (a == "-autotest-timeout") int.TryParse(args[++i], out autotestTimeout);
                else if (a == "-autotest-noexit") autotestNoExit = true;
                else if (a.StartsWith("-autotest\"")) autotestHouses = a.Substring(10).Trim('\"');
                else if (a.StartsWith("-autotest-opts\"")) autotestChecks = a.Substring(15).Trim('\"');
                else if (a.StartsWith("-autotest-timeout\"")) { int.TryParse(a.Substring(18).Trim('\"'), out autotestTimeout); autotestEnabled = true; }
                else if (a == "-textdiag") textdiagEnabled = true;
            }


            foreach (var arg in args)
            {
                if (arg.Length > 0 && arg[0] == '-')
                {
                    var cmd = arg.Substring(1);
                    if (cmd.StartsWith("lang"))
                    {
                        // AUD-17 G-9: bare/malformed -lang and -hz used to throw
                        // an unhandled FormatException before any UI existed.
                        if (!byte.TryParse(cmd.Substring(4), out var langCode))
                            Console.WriteLine("Warning: -lang expects a number; ignoring '" + arg + "'");
                        else
                            GlobalSettings.Default.LanguageCode = langCode;
                    }
                    else if (cmd.StartsWith("hz"))
                    {
                        if (!int.TryParse(cmd.Substring(2), out var hz))
                            Console.WriteLine("Warning: -hz expects a number; ignoring '" + arg + "'");
                        else
                            GlobalSettings.Default.TargetRefreshRate = hz;
                    }
                    else
                    {
                        //normal style param
                        switch (cmd)
                        {
                            case "ide":
                                ide = true;
                                break;
                            case "3d":
                                FSOEnvironment.Enable3D = true;
                                break;
                            case "aa":
                                aa = true;
                                break;
                            case "jit":
                                jit = true;
                                break;
                            case "dx":
                            case "dx11":
                                // Allow forcing DX on Windows even in Desktop build
                                if (!linux)
                                    useDX = true;
                                else
                                    Console.WriteLine("Warning: DirectX is not available on Linux/macOS. Using OpenGL.");
                                break;
                            case "gl":
                            case "ogl":
                                useDX = false;
                                break;
                            case "touch":
                                FSOEnvironment.SoftwareKeyboard = true;
                                break;
                            case "nosound":
                                FSOEnvironment.NoSound = true;
                                break;
                            case string s when s.StartsWith("path"): //The Sims path (highest priority)
                                if (s.Length > 4)
                                {
                                    path = s.Substring(4).Trim('"').Replace('\\', '/');
                                    if (!path.EndsWith("/")) path += "/";
                                    pathProvidedViaCommandLine = true;
                                }
                                break;
                        }
                    }
                }
            }
            #endregion

            // If no path provided via command line, check if we need to configure installation
            if (!pathProvidedViaCommandLine)
            {
                // Check if installation has already been configured
                if (!GlobalSettings.Default.TS1InstallationConfigured || string.IsNullOrEmpty(path))
                {
                    var installations = gameLocator.GetAllTheSims1Installations();

                    if (installations.Count == 0)
                    {
                        // No installations found - show GUI to allow browsing
                        Console.WriteLine("No installations auto-detected. Opening selection dialog...");
                        
                        try
                        {
                            // Save current directory - Eto.Forms file dialogs on Linux (GTK) can change the working directory
                            var savedDir = Directory.GetCurrentDirectory();
                            
                            InstallationSelectionResult? result = null;
                            using (var app = new Application(Eto.Platform.Detect))
                            {
                                using (var dialog = new InstallationSelectorDialog(new System.Collections.Generic.List<InstallationInfo>()))
                                {
                                    result = dialog.ShowModal();
                                    dialog.Visible = false;
                                }
                                // Process pending events to ensure dialog is fully closed
                                app.RunIteration();
                            }
                            
                            // Restore current directory after dialog closes
                            Directory.SetCurrentDirectory(savedDir);

                            if (result != null)
                            {
                                // Normalize path - ensure it ends with /
                                path = result.Path.Replace('\\', '/');
                                if (!path.EndsWith("/")) path += "/";
                                SaveInstallationConfig(path, result.IsSteam);
                                Console.WriteLine($"Selected: {path}");
                            }
                            else
                            {
                                Console.WriteLine("Installation selection cancelled.");
                                Environment.Exit(0);
                            }
                        }
                        catch (Exception ex)
                        {
                            // GUI failed, show console error
                            Console.WriteLine("GUI unavailable. Could not find The Sims 1 installation.");
                            Console.WriteLine("Please use the -path argument to specify the location:");
                            Console.WriteLine("  ./TheSims -path\"/path/to/The Sims/\"");
                            Console.WriteLine();
                            Console.WriteLine("Common locations:");
                            if (linux && Directory.Exists("/Users"))
                            {
                                Console.WriteLine("  Steam: ~/Library/Application Support/Steam/steamapps/common/The Sims/");
                                Console.WriteLine("  Wine: ~/.wine/drive_c/Program Files/Maxis/The Sims/");
                            }
                            else if (linux)
                            {
                                Console.WriteLine("  Steam Play/Proton: ~/.steam/steam/steamapps/common/The Sims/");
                                Console.WriteLine("  Wine: ~/.wine/drive_c/Program Files/Maxis/The Sims/");
                            }
                            Environment.Exit(1);
                        }
                    }
                    else if (installations.Count == 1)
                    {
                        // Single installation - auto-select
                        path = installations[0].path;
                        var isSteam = installations[0].type == TS1InstallationType.Steam;
                        Console.WriteLine($"Auto-detected installation: {installations[0].description}");
                        Console.WriteLine($"Path: {path}");
                        SaveInstallationConfig(path, isSteam);
                    }
                    else
                    {
                        // Multiple installations - show GUI selector
                        var installInfos = installations
                            .Select(i => new InstallationInfo(i.description, i.path, i.type))
                            .ToList();

                        Console.WriteLine($"Found {installations.Count} installations. Showing selection dialog...");
                        
                        try
                        {
                            // Save current directory - Eto.Forms file dialogs on Linux (GTK) can change the working directory
                            var savedDir = Directory.GetCurrentDirectory();
                            
                            InstallationSelectionResult? result = null;
                            using (var app = new Application(Eto.Platform.Detect))
                            {
                                using (var dialog = new InstallationSelectorDialog(installInfos))
                                {
                                    result = dialog.ShowModal();
                                    dialog.Visible = false;
                                }
                                // Process pending events to ensure dialog is fully closed
                                app.RunIteration();
                            }
                            
                            // Restore current directory after dialog closes
                            Directory.SetCurrentDirectory(savedDir);

                            if (result != null)
                            {
                                // Normalize path - ensure it ends with /
                                path = result.Path.Replace('\\', '/');
                                if (!path.EndsWith("/")) path += "/";
                                SaveInstallationConfig(path, result.IsSteam);
                                Console.WriteLine($"Selected: {path}");
                            }
                            else
                            {
                                Console.WriteLine("Installation selection cancelled.");
                                Environment.Exit(0);
                            }
                        }
                        catch (Exception ex)
                        {
                            // GUI failed, fall back to console selection
                            Console.WriteLine("GUI unavailable, using console selection:");
                            Console.WriteLine();
                            for (int i = 0; i < installations.Count; i++)
                            {
                                Console.WriteLine($"[{i + 1}] {installations[i].description}");
                                Console.WriteLine($"    {installations[i].path}");
                            }
                            Console.WriteLine();
                            Console.Write($"Select installation (1-{installations.Count}): ");
                            
                            if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 1 && choice <= installations.Count)
                            {
                                path = installations[choice - 1].path;
                                SaveInstallationConfig(path, installations[choice - 1].type == TS1InstallationType.Steam);
                                Console.WriteLine($"Selected: {path}");
                            }
                            else
                            {
                                Console.WriteLine("Invalid selection.");
                                Environment.Exit(1);
                            }
                        }
                    }
                }
            }
            
            useDX = MonogameLinker.Link(useDX);

            // load DPI scale factor from config.ini 
            // users can customize via config.ini: (e.g. DPIScaleFactor=1.25 for 125% scaling)
            FSOEnvironment.DPIScaleFactor = GlobalSettings.Default.DPIScaleFactor;

            FSO.Files.ImageLoaderHelpers.BitmapFunction = BitmapReader;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            FSOEnvironment.SoftwareDepth = false;
            // MRT needs DirectX; on OpenGL backends (macOS/Linux) disable like the iOS/Droid
            // builds do — Apple's GL driver flagged the MRT texture path (GLD_TEXTURE_INDEX_2D).
            FSOEnvironment.UseMRT = useDX;

            if (path != null)
            {
                FSOEnvironment.ContentDir = "Content/";
                FSOEnvironment.GFXContentDir = "Content/" + (useDX ? "DX/" : "OGL/");
                FSOEnvironment.Linux = linux;
                FSOEnvironment.DirectX = useDX;
                FSOEnvironment.GameThread = Thread.CurrentThread;
                if (GlobalSettings.Default.LanguageCode == 0) GlobalSettings.Default.LanguageCode = 1;
                FSO.Files.Formats.IFF.Chunks.STR.DefaultLangCode = (FSO.Files.Formats.IFF.Chunks.STRLangCode)GlobalSettings.Default.LanguageCode;

                GlobalSettings.Default.StartupPath = path;
                GlobalSettings.Default.TS1HybridEnable = true;
                GlobalSettings.Default.TS1HybridPath = path;
                GlobalSettings.Default.ClientVersion = "0";
                GlobalSettings.Default.LightingMode = 3;
                GlobalSettings.Default.AntiAlias = aa ? 1 : 0;
                GlobalSettings.Default.ComplexShaders = true;
                GlobalSettings.Default.EnableTransitions = true;

                if (ide)
                {
                    Console.WriteLine("Warning: Volcanic IDE is not available on this platform.");
                    Console.WriteLine("The IDE requires Windows. Continuing without IDE support.");
                }

                var assemblies = new FSO.SimAntics.JIT.Runtime.AssemblyStore();
                if (jit) assemblies.InitAOT();
                FSO.SimAntics.Engine.VMTranslator.INSTANCE = new FSO.SimAntics.JIT.Runtime.VMAOTTranslator(assemblies);


                // Headless -autotest: register the engine-side fidelity checks before the
                // game loop runs; driven entirely from the game thread via GameThread hooks.
                if (autotestEnabled)
                {
                    Console.WriteLine("AUTOTEST enabled houses=" + (autotestHouses ?? "(default)") + " checks=" + (autotestChecks ?? "(default)"));
                    Simitone.Client.AutotestRunner.Begin(autotestHouses, autotestChecks, autotestTimeout, !autotestNoExit);
                }

                // R138 -textdiag: pixel-level text rendering diagnostics (prints
                // ASCII art of the rendered glyphs to stdout, then exits).
                if (textdiagEnabled)
                {
                    Simitone.Client.TextDiagnostics.Begin();
                }

                Simitone.Client.Utils.MacSDLClipboard.Install();

                var start = new GameStartProxy();
                try
                {
                    start.Start(useDX);
                }
                catch (Exception ex)
                {
                    CrashLog("Unhandled exception in game loop: " + ex);
                    Console.Error.WriteLine("FATAL: " + ex);
                    throw;
                }
            }
            else
            {
                Console.WriteLine("Error: Could not find The Sims 1 installation.");
                Console.WriteLine("Please use the -path argument to specify the location:");
                Console.WriteLine("  ./TheSims -path\"/path/to/The Sims/\"");
                Console.WriteLine();
                Console.WriteLine("Common locations:");
                Console.WriteLine("  Steam Play/Proton: ~/.steam/steam/steamapps/common/The Sims/");
                Console.WriteLine("  Wine: ~/.wine/drive_c/Program Files/Maxis/The Sims/");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Save installation configuration to settings
        /// </summary>
        private static void SaveInstallationConfig(string path, bool isSteam)
        {
            GlobalSettings.Default.TS1HybridPath = path;
            GlobalSettings.Default.TS1IsSteamInstall = isSteam;
            GlobalSettings.Default.TS1InstallationConfigured = true;
            GlobalSettings.Default.Save();
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var name = args.Name;
                if (name.StartsWith("FSO.Scripts"))
                {
                    return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == name);
                }
                else
                {
                    var assemblyName = args.Name.Substring(0, name.IndexOf(',')) + ".dll";
                    // Use absolute path based on application base directory
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var assemblyPath = Path.Combine(baseDir, MonogameLinker.AssemblyDir, assemblyName);
                    
                    if (File.Exists(assemblyPath))
                    {
                        var assembly = Assembly.LoadFrom(assemblyPath);
                        return assembly;
                    }
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject;
            CrashLog("FATAL: " + (exception != null ? exception.ToString() : "unknown unhandled exception"));
            if (exception is OutOfMemoryException)
            {
                Console.Error.WriteLine("=== FATAL ERROR ===");
                Console.Error.WriteLine("Out of Memory! The Sims needs to close.");
                Console.Error.WriteLine(e.ExceptionObject.ToString());
            }
            else
            {
                Console.Error.WriteLine("=== FATAL ERROR ===");
                Console.Error.WriteLine("A fatal error occurred! Please report this issue on GitHub or Discord.");
                Console.Error.WriteLine(e.ExceptionObject.ToString());
            }
        }

        private static void CrashLog(string message)
        {
            // R145a: write BOTH next to the binary and into the UserDir — a
            // deploy rsync --delete over the app bundle silently destroyed the
            // one crash-log copy we had (the 2026-08-27 SIGABRT left no trace).
            foreach (var dir in new[] { AppDomain.CurrentDomain.BaseDirectory, FSOEnvironment.UserDir })
            {
                try
                {
                    var file = Path.Combine(dir, "thesims-crash.log");
                    File.AppendAllText(file, DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
                }
                catch
                {
                }
            }
        }

        public static Tuple<byte[], int, int> BitmapReader(Stream str)
        {
            // Use ImageSharp for cross-platform image loading
            // System.Drawing.Common has issues with libgdiplus on Linux
            //
            // Windows GDI+ loads images as BGRA in memory (called "ARGB" confusingly)
            // then applies RGB->BGR swap, resulting in RGBA in memory.
            // We load directly as RGBA to match that final result.
            using (var image = Image.Load<Rgba32>(str))
            {
                var data = new byte[image.Width * image.Height * 4];
                image.CopyPixelDataTo(data);
                return new Tuple<byte[], int, int>(data, image.Width, image.Height);
            }
        }
    }
}