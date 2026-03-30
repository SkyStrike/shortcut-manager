using Microsoft.UI.Xaml;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ShortcutManager
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window _window;

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            // Initialize Logging from external configuration
            string logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("serilog.json", optional: false, reloadOnChange: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            // Set up global exception handling
            this.UnhandledException += App_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unhandled UI Exception");
            ShowFatalError("A UI error occurred", e.Exception);
            e.Handled = true; // Attempt to recover
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unobserved Task Exception");
            ShowFatalError("A background task error occurred", e.Exception);
            e.SetObserved();
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Error(ex, "Unhandled Domain Exception (IsTerminating: {IsTerminating})", e.IsTerminating);
                ShowFatalError("A critical system error occurred", ex);
            }
        }

        private void ShowFatalError(string context, Exception ex)
        {
            string message = $"{context}:\n\n{ex.Message}\n\nThe application will attempt to continue, but it may be unstable. Please check the logs for more details.";
            
            // Use Win32 MessageBox as it's more reliable than XAML dialogs during crashes
            // 0x00000010L is MB_ICONERROR
            MessageBox(IntPtr.Zero, message, "Shortcut Manager - Error", 0x00000010);
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // Environment.GetCommandLineArgs is often more reliable for unpackaged apps
                var commandLineArgs = Environment.GetCommandLineArgs();
                Log.Information("CommandLineArgs: {Args}", string.Join(" ", commandLineArgs));
                Log.Information("WinUI Args: {Args}", args.Arguments);

                bool hasHiddenArg = (args.Arguments != null && (args.Arguments.Contains("-hidden") || args.Arguments.Contains("/hidden"))) ||
                                     commandLineArgs.Any(a => a.Equals("-hidden", StringComparison.OrdinalIgnoreCase) || 
                                                             a.Equals("/hidden", StringComparison.OrdinalIgnoreCase));

                _window = new MainWindow(hasHiddenArg);

                if (hasHiddenArg)
                {
                    // We don't call Activate() so the window remains hidden
                    Log.Information("Application started hidden (background mode)");
                }
                else
                {
                    _window.Activate();
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical Error during OnLaunched");
                Log.CloseAndFlush(); // Ensure logs are written to disk before exit
                ShowFatalError("Startup Error", ex);
                // Exit as startup failed
                Environment.Exit(1);
            }
        }
    }
}
