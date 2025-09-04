using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Management;
using System.Reflection;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Specialized;
using System.Threading;
using System.Diagnostics;
using Broker;
using Aladdin.HASP;
//using NDepend.Helpers.FileDirectoryPath;
using InrEditor;
using Utility;
using CommandLine;
using Microsoft.VisualBasic.ApplicationServices;
using Utility.Ray;

namespace ASAPGUI
{
    //=======================================================================
    static class Program
    {
        //===================================================================
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main( string[] args )
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault( false );
            ASAPSingleInstanceController sic = new ASAPSingleInstanceController();
            sic.Run( args );
        }
    }

    //=======================================================================
    public class ASAPSingleInstanceController : WindowsFormsApplicationBase
    {
        //===================================================================
        string licenseType = "trial";

        //===================================================================
        DateTime dateExpire = new DateTime();

        //===================================================================
        DateTime supportExpire = new DateTime();

        //===================================================================
        DateTime dt = new DateTime( 1970, 1, 1, 0, 0, 0 );

        //===================================================================
        public static int mAssemblyLoadErrors = 0;

        //===================================================================
        public static ASAP.Splash Splash { get; set; }

        //===================================================================
        public static bool ShowSplash { get; set; }

        //===================================================================
        string[] StartupArgs { get; set; }

        //===================================================================
        public SentinelHASP_ASAPNG HaspSentinel { get; set; }

        //===================================================================
        string ConsoleOutputFile { get; set; }

        //===================================================================
        static ASAPSingleInstanceController()
        {
        }

        //===================================================================
        public ASAPSingleInstanceController()
        {
            HaspSentinel = null;

            // Set whether the application is single instance
            this.IsSingleInstance = true;
            this.StartupNextInstance += new StartupNextInstanceEventHandler( onStartNextInstance );
            this.Startup += new StartupEventHandler( onStartup );
            this.Shutdown += new ShutdownEventHandler( onShutdown );
        }

        //===================================================================
        void onShutdown( object sender, EventArgs e )
        {
            if ( HaspSentinel != null )
            {
                HaspSentinel.Logout();
            }
            Utility.Registry.CopyUserKey( "Software\\Breault Research Organization\\ASAP", "Software\\Breault Research Organization\\ASAP.bak" );
        }

        //===================================================================
        /// <summary>
        /// Perform all initialization for singleton here
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void onStartup( object sender, StartupEventArgs e )
        {
            Utility.Diagnostics.ErrorLogger.Instance.AddLogger(new Utility.Diagnostics.TextFileLog(1000));

            StartupArgs = e.CommandLine.ToArray();

            HaspSentinel = new SentinelHASP_ASAPNG();

            HaspStatus haspStatus = HaspStatus.LocalCommErr;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.TerminalServiceDetected;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.VMDetected;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.InvalidSignature;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.CorruptStorage;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.InternalError;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.HaspDotNetDllBroken;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.EmptyScopeResults;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.FeatureNotFound;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.ContainerNotFound;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.BrokenSession;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.SystemError;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspStatus.TimeError;
            HaspSentinel.DisplayStatus(haspStatus, true);

            haspStatus = HaspSentinel.GetProductStatus( SentinelHASP_ASAPNG.ASAP_FEATURE.ASAP );

            if ( haspStatus != HaspStatus.StatusOk )
            {
                HaspSentinel.DisplayStatus( haspStatus, true );
                
                Broker.frmActivation_ASAP frm = new frmActivation_ASAP( true );
                
                frm.ShowDialog();
                
                haspStatus = HaspSentinel.GetProductStatus( SentinelHASP_ASAPNG.ASAP_FEATURE.ASAP );

                if ( haspStatus != HaspStatus.StatusOk )
                {
                    e.Cancel = true;
                
                    return;
                }
            }
            else
            { 
                DateTime linkerTime = GetLinkerTime( Assembly.GetExecutingAssembly() ); 
                licenseType = HaspSentinel.GetLicenseTypeAll( (int)SentinelHASP_ASAPNG.ASAP_FEATURE.ASAP );
                // DEV_NOTE 250212.4 new license checker
                bool supportCheck = HaspSentinel.getSupportStatus(); // set during license check
                dateExpire = HaspSentinel.getCurrentFeatureEXP(); // if perpetual then gives support exp
                
                if ( supportCheck == false && dateExpire < linkerTime )
                { //support is bad and exp of expiration,trial, or support is before today
                    MessageBox.Show( "Support has expired.\nThis version of ASAP requires an updated maintenance agreement." );
                    
                    Broker.frmActivation_ASAP frm = new frmActivation_ASAP( true );
                    
                    frm.ShowDialog();
                                        
                    haspStatus = HaspSentinel.GetProductStatus( SentinelHASP_ASAPNG.ASAP_FEATURE.ASAP );
                    // perpetual is still status ok
                    if ( haspStatus != HaspStatus.StatusOk )
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
            // Force console output to a file
            FileStream fs = null;

            StreamWriter sw = null;

            ConsoleOutputFile = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ),
                                                                         "Breault Research Organization\\ASAP",
                                                                         "Console.out" );

            try
            {
                if ( Debugger.IsAttached == false )
                {
                    ConsoleOutputFile = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ),
                                                      "Breault Research Organization\\ASAP",
                                                      "Console.out" );

                    fs = new FileStream( ConsoleOutputFile, FileMode.Create );

                    sw = new StreamWriter( fs, Encoding.UTF8 );

                    sw.AutoFlush = true;

                    Console.SetOut( sw );
                }
            }
            catch ( Exception ex )
            {
                Utility.Diagnostics.ErrorLogger.Instance.IgnoreException( ex );
            }

            try
            {
                // Get number of hardware cores
                int nCores = Environment.ProcessorCount;

                // Set affinity to the first two cores
                Process proc = Process.GetCurrentProcess();

                long AffinityMask = (long)proc.ProcessorAffinity;

                if ( nCores > 2 )
                {
                    // use any of the first 2 available processors
                    AffinityMask &= 0x0003;
                }
                else
                {
                    // use only the first available processor
                    AffinityMask &= 0x0001;
                }

                proc.ProcessorAffinity = (IntPtr)AffinityMask;
            }
            catch ( Exception ex )
            {
                Console.WriteLine( "P1: " + ex.Message );
            }

            // MPA: Don't Use INI files for settings storage
            Utility.Registry.LocalIni = false;

            Utility.Registry.UserIni = false;
            
            Utility.Registry.CopyIfMissing( "Software\\Breault Research Organization\\ASAP.bak", "Software\\Breault Research Organization\\ASAP" );

            ASAPGUI.ASAPPaths.SetPaths();

            // Initialize nevron license
            try
            {
                Graphs.GraphUtils.InitializeNevronLicense();
            }
            catch ( Exception ex )
            {
                MessageBox.Show( "The Charting software license failed to initialize.\n" +
                                 "Please contact Technical Support for help with this problem." );

                Console.Write( "P2: " + ex.Message );
            }

            // Force some of the referenced assemblies to initialize their static members
            InrEditor.InrEditorControl.ForceInit = true;

            // Check to see if we have projects in public\documents
            string publicProjectsDir = Environment.GetFolderPath( Environment.SpecialFolder.CommonDocuments );

            string programProjectsDir = Path.Combine( ASAPGUI.ASAPPaths.AsapInstallPath, "Projects" );
            
            publicProjectsDir = Path.Combine( publicProjectsDir, "Breault Research Organization", "Projects" );

            if ( Directory.Exists( publicProjectsDir ) == false )
            {
                if ( Splash != null && Splash.Visible )
                {
                    Splash.SplashLabel.Text = "Copying project files to public folder...";

                    Splash.Update();
                }
                DirectoryCopy( programProjectsDir, publicProjectsDir, true );
            }
        }

        //===================================================================
        // We only allow one instance, so open any requested files here
        void onStartNextInstance( object sender, StartupNextInstanceEventArgs e )
        {
            frmMain mainForm = MainForm as frmMain;

            if ( mainForm != null )
            {
                ASAPArguments parsedArgs = new ASAPArguments();

                if ( CommandLine.Parser.ParseArguments( e.CommandLine.ToArray(), parsedArgs ) )
                {
                    if ( parsedArgs.files != null )
                    {
                        foreach ( string file in parsedArgs.files )
                        {
                            mainForm.FilesToOpenAtStart.Add( file );
                        }
                    }
                }
            }
        }

        //===================================================================
        protected override void OnCreateMainForm()
        {
            // Instantiate your main application form
            frmMain mainForm = new frmMain();
            mainForm.HaspSentinel = this.HaspSentinel;
            this.MainForm = mainForm;

            // Fill in asap version label area
            if ( licenseType == "trial" )
            {
                mainForm.cmdStatusVersion.Text = string.Format( "ASAP trial expires: {0}", dateExpire.ToShortDateString() );
            }
            else
            {
                mainForm.cmdStatusVersion.Visible = false;
            }

            ASAPArguments parsedArgs = new ASAPArguments();

            if ( CommandLine.Parser.ParseArguments( StartupArgs, parsedArgs ) )
            {
                ASAPData.ASAPConfig config = new ASAPData.ASAPConfig();
                config.LoadConfigFile();
                ShowSplash = config.LoadBoolPreference( true, "GeneralPreferences", "ShowSplash" );
                ShowSplash = Debugger.IsAttached == false ? ShowSplash : false;
                ShowSplash &= parsedArgs.noSplash == false;

                Splash = null;

                if ( ShowSplash )
                {
                    int nextSplash = config.LoadIntPreference( 0, "GeneralPreferences", "NextSplash" );
                    config.SaveConfig( "GeneralPreferences", "NextSplash", ( nextSplash + 1 ).ToString() );
                    Splash = new ASAP.Splash();
                    Splash.SetImageIndex( nextSplash );
                    Splash.TopMost = true;
                    Splash.Show( mainForm );
                    Application.DoEvents();
                    Splash.Update();

                    try
                    {
                        AppDomain.CurrentDomain.AssemblyLoad += new AssemblyLoadEventHandler( ShowAssemblies );
                    }
                    catch ( Exception ex )
                    {
                        Utility.Diagnostics.ErrorLogger.Instance.IgnoreException( ex );
                    }
                }
                else
                {
                    try
                    {
                        foreach ( Assembly assembly in AppDomain.CurrentDomain.GetAssemblies() )
                        {
                            try
                            {
                                LoadReferencedAssembly( assembly );
                            }
                            catch ( Exception ex )
                            {
                                mAssemblyLoadErrors++;
                                Console.WriteLine( "P3: " + ex.Message );
                            }
                        }
                    }
                    catch ( Exception ex )
                    {
                        Utility.Diagnostics.ErrorLogger.Instance.IgnoreException( ex );
                    }
                }

                // Force loading of all assemblies, 
                // so program will be snappy
                if ( System.Diagnostics.Debugger.IsAttached == false )
                {
                    int nErrors = 0;

                    foreach ( Assembly assembly in AppDomain.CurrentDomain.GetAssemblies() )
                    {
                        try
                        {
                            LoadReferencedAssembly( assembly );
                        }
                        catch ( Exception ex )
                        {
                            mAssemblyLoadErrors++;
                            Console.WriteLine( "P4: " + ex.Message );
                        }
                    }
                    if ( mAssemblyLoadErrors > 0 )
                    {
                        string msg = "";

                        if ( nErrors == 1 )
                        {
                            msg = string.Format( "{0} assembly failed to load during initialization.\nRefer to the file: \n{1}\nfor details.",
                                                 mAssemblyLoadErrors,
                                                 ConsoleOutputFile );
                        }
                        else
                        {
                            msg = string.Format( "{0} assemblies failed to load during initialization.\nRefer to the file: \n{1}\nfor details.",
                                                 mAssemblyLoadErrors,
                                                 ConsoleOutputFile );
                        }

                        DialogResult ret = MessageBox.Show( msg, "ASAP", MessageBoxButtons.OKCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1 );

                        if ( ret == DialogResult.Cancel )
                        {
                            return;
                        }
                    }
                }

                // Load files specified on command line
                if ( parsedArgs.files != null )
                {
                    foreach ( string file in parsedArgs.files )
                    {
                        mainForm.FilesToOpenAtStart.Add( file );
                    }
                }
            }
        }

        //===================================================================
        public static DateTime GetLinkerTime( Assembly assembly, TimeZoneInfo target = null )
        {
            var filePath = assembly.Location;

            const int c_PeHeaderOffset = 60;
            
            const int c_LinkerTimestampOffset = 8;

            var buffer = new byte[2048];

            using ( var stream = new FileStream( filePath, FileMode.Open, FileAccess.Read ) )
            {
                stream.Read( buffer, 0, 2048 );
            }

            var offset = BitConverter.ToInt32( buffer, c_PeHeaderOffset );
            
            var secondsSince1970 = BitConverter.ToInt32( buffer, offset + c_LinkerTimestampOffset );
            
            var epoch = new DateTime( 1970, 1, 1, 0, 0, 0, DateTimeKind.Utc );

            var linkTimeUtc = epoch.AddSeconds( secondsSince1970 );

            var tz = target ?? TimeZoneInfo.Local;
            
            var localTime = TimeZoneInfo.ConvertTimeFromUtc( linkTimeUtc, tz );

            return localTime;
        }

        //===================================================================
        private static void DirectoryCopy( string sourceDirName, string destDirName, bool copySubDirs )
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo( sourceDirName );

            if ( !dir.Exists )
            {
                throw new DirectoryNotFoundException( "Source directory does not exist or could not be found: " + sourceDirName );
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.
            if ( !Directory.Exists( destDirName ) )
            {
                Directory.CreateDirectory( destDirName );
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            
            foreach ( FileInfo file in files )
            {
                string temppath = Path.Combine( destDirName, file.Name );
            
                file.CopyTo( temppath, false );
            }

            // If copying subdirectories, copy them and their contents to new location.
            if ( copySubDirs )
            {
                foreach ( DirectoryInfo subdir in dirs )
                {
                    string temppath = Path.Combine( destDirName, subdir.Name );

                    DirectoryCopy( subdir.FullName, temppath, copySubDirs );
                }
            }
        }

        //===================================================================
        private static void LoadReferencedAssembly( Assembly assembly )
        {
            Console.WriteLine( "P5: " + assembly.FullName );

            foreach ( AssemblyName name in assembly.GetReferencedAssemblies() )
            {
                if ( !AppDomain.CurrentDomain.GetAssemblies().Any( a => a.FullName == name.FullName ) )
                {
                    try
                    {
                        LoadReferencedAssembly( Assembly.Load( name ) );
                    }
                    catch ( Exception ex )
                    {
                        mAssemblyLoadErrors++;

                        Console.WriteLine( "P6: " + ex.Message );
                    }
                }
            }

            try
            {
                Assembly.Load( assembly.FullName );
            }
            catch ( Exception ex )
            {
                mAssemblyLoadErrors++;

                Console.WriteLine( "P7: " + ex.Message );
            }
        }

        //===================================================================
        private static void ShowAssemblies( object sender, AssemblyLoadEventArgs e )
        {
            if ( Splash.Visible )
            {
                Splash.SplashLabel.Text = "Loading Assembly - " + e.LoadedAssembly.FullName;

                Splash.Update();
            }
        }
    }

    //=======================================================================
    class ASAPArguments
    {
        //===================================================================
        [Argument( ArgumentType.AtMostOnce, HelpText = "Don't show splash screen at startup.", DefaultValue = false )]
        public bool noSplash;

        //===================================================================
        [DefaultArgument( ArgumentType.MultipleUnique, HelpText = "Input file(s)." )]
        public string[] files;

    }
}
