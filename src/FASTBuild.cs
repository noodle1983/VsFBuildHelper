// Copyright 2016 Liam Flookes and Yassine Riahi
// Available under an MIT license. See license file on github for details.

using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;

using System.Reflection;
using System.IO;
using EnvDTE;
using EnvDTE80;

namespace VsFBuildHelper
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class FASTBuild
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;
		public const int SlnCommandId = 0x0101;
		public const int ContextCommandId = 0x0102;
		public const int SlnContextCommandId = 0x0103;

		/// <summary>
		/// Command menu group (command set GUID).
		/// </summary>
		public static readonly Guid CommandSet = new Guid("7c132991-dea1-4719-8c67-c20b24b6775c");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        private static System.Diagnostics.Process FBProcess;
        public static bool IsFbProcessRunning() { return FBProcess != null && !FBProcess.HasExited; }
        public static void StopFbProcess() {
            if (IsFbProcessRunning()) { FBProcess.Kill(); }
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="FASTBuild"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private FASTBuild(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);

				menuCommandID = new CommandID(CommandSet, SlnCommandId);
				menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
				commandService.AddCommand(menuItem);

				menuCommandID = new CommandID(CommandSet, ContextCommandId);
				menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
				commandService.AddCommand(menuItem);

				menuCommandID = new CommandID(CommandSet, SlnContextCommandId);
				menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
				commandService.AddCommand(menuItem);
			}
		}

		/// <summary>
		/// Gets the instance of the command.
		/// </summary>
		public static FASTBuild Instance
        {
            get;
            private set;
        }

		public FASTBuildPackage Package
        {
            get
            {
				return (FASTBuildPackage)this.package;
            }
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new FASTBuild(package);
        }

		[System.Runtime.InteropServices.DllImport("kernel32.dll")]
		public static extern int GetSystemDefaultLCID();

		private static bool IsFBuildFindable(string FBuildExePath)
		{
			//string fbuild = "fbuild.exe";
            string fbuild = "C:\\tools\\fastbuild\\FBuild.exe";

			if (FBuildExePath != fbuild)
			{
				return File.Exists(FBuildExePath);
			}

            return File.Exists(fbuild);
			//string PathVariable = Environment.GetEnvironmentVariable("PATH");
			//foreach (string SearchPath in PathVariable.Split(Path.PathSeparator))
			//{
			//	try
			//	{
			//		string PotentialPath = Path.Combine(SearchPath, fbuild);
			//		if (File.Exists(PotentialPath))
			//		{
			//			return true;
			//		}
			//	}
			//	catch (ArgumentException)
			//	{ }
			//}
			//return false;
		}

		/// <summary>
		/// This function is the callback used to execute the command when the menu item is clicked.
		/// See the constructor to see how the menu item is associated with this function using
		/// OleMenuCommandService service and MenuCommand class.
		/// </summary>
		/// <param name="sender">Event sender.</param>
		/// <param name="e">Event args.</param>
		private void MenuItemCallback(object sender, EventArgs e)
        {
            FASTBuildPackage fbPackage = (FASTBuildPackage)this.package;
            if (null == fbPackage.dte.Solution)
                return;

            MenuCommand eventSender = sender as MenuCommand;

            if (eventSender == null)
            {
                fbPackage.outputPane.OutputString("VSIX failed to cast sender to OleMenuCommand.\r");
                return;
            }


            Solution sln = fbPackage.dte.Solution;
            SolutionBuild sb = sln.SolutionBuild;
            SolutionConfiguration2 sc = sb.ActiveConfiguration as SolutionConfiguration2;
            VCProject proj = null;

            if (eventSender.CommandID.ID != SlnCommandId && eventSender.CommandID.ID != SlnContextCommandId)
            {
                if (fbPackage.dte.SelectedItems.Count > 0)
                {
                    Project envProj = (fbPackage.dte.SelectedItems.Item(1).Project as EnvDTE.Project);
                    if (envProj != null)
                    {
                        proj = envProj.Object as VCProject;
                    }
                }

                if (proj == null)
                {
                    string startupProject = "";
                    foreach (String item in (Array)sb.StartupProjects)
                    {
                        startupProject += item;
                    }
                    proj = sln.Item(startupProject).Object as VCProject;
                }

                if (proj == null)
                {
                    fbPackage.outputPane.OutputString("No valid vcproj selected for building or set as the startup project.\r");
                    return;
                }

                RunfastbuildOnProject(proj);
            }
            else
            {
                RunfastbuildOnSln();
            }
        }

        public static void RunfastbuildOnProject(VCProject proj)
        {
            FASTBuildPackage fbPackage = (FASTBuildPackage)Instance.package;
            if (null == fbPackage.dte.Solution)
                return;

            Solution sln = fbPackage.dte.Solution;
            SolutionBuild sb = sln.SolutionBuild;
            SolutionConfiguration2 sc = sb.ActiveConfiguration as SolutionConfiguration2;
            fbPackage.outputPane.OutputString("Building " + Path.GetFileName(proj.ProjectFile) + " " + sc.Name + " " + sc.PlatformName + "\r");
            string fbCommandLine = string.Format("fbuild.exe --dist"); //, Path.GetFileName(proj.ProjectFile), sc.Name, sc.PlatformName, sln.FileName, fbPackage.FBPath);
            string fbWorkingDirectory = Path.GetDirectoryName(proj.ProjectFile);

            Runfastbuild(fbPackage, fbCommandLine, fbWorkingDirectory);
        }

        public static void RunfastbuildOnSln()
        {
            FASTBuildPackage fbPackage = (FASTBuildPackage)Instance.package;
            if (null == fbPackage.dte.Solution)
                return;

            Solution sln = fbPackage.dte.Solution;
            SolutionBuild sb = sln.SolutionBuild;
            SolutionConfiguration2 sc = sb.ActiveConfiguration as SolutionConfiguration2;
            string fbCommandLine = "fbuild.exe -dist";//string.Format("-s \"{0}\" -c {1} -f {2} -a\"{3}\" -b \"{4}\"", sln.FileName, sc.Name, sc.PlatformName, fbPackage.OptionFBArgs, fbPackage.FBPath);
            string fbWorkingDirectory = Path.GetDirectoryName(sln.FileName);

            Runfastbuild(fbPackage, fbCommandLine, fbWorkingDirectory);
        }
        public static void RunCleanOnSln()
        {
            // select and clean
        }


        public static void Runfastbuild(FASTBuildPackage fbPackage, string fbCommandLine, string fbWorkingDirectory)
        {
            fbPackage.outputPane.Activate();
            fbPackage.outputPane.Clear();


            if (fbPackage.dte.Debugger.CurrentMode != dbgDebugMode.dbgDesignMode)
            {
                fbPackage.outputPane.OutputString("Build not launched due to active debugger.\r");
                return;
            }

            if (!IsFBuildFindable(fbPackage.FBPath))
            {
                fbPackage.outputPane.OutputString(string.Format("Could not find fbuild at the provided path: {0}, please verify in the fastbuild options.\r", fbPackage.FBPath));
                return;
            }

            fbPackage.dte.ExecuteCommand("File.SaveAll");

            try
            {
                fbPackage.outputPane.OutputString("Launching fastbuild with command line: " + fbCommandLine + "\r");

                FBProcess = new System.Diagnostics.Process();
                FBProcess.StartInfo.FileName = fbPackage.FBPath;
                FBProcess.StartInfo.Arguments = fbCommandLine;
                FBProcess.StartInfo.WorkingDirectory = fbWorkingDirectory;
                FBProcess.StartInfo.RedirectStandardOutput = true;
                FBProcess.StartInfo.UseShellExecute = false;
                FBProcess.StartInfo.CreateNoWindow = true;
                var SystemEncoding = System.Globalization.CultureInfo.GetCultureInfo(GetSystemDefaultLCID()).TextInfo.OEMCodePage;
                FBProcess.StartInfo.StandardOutputEncoding = System.Text.Encoding.GetEncoding(SystemEncoding);

                System.Diagnostics.DataReceivedEventHandler OutputEventHandler = (Sender, Args) =>
                {
                    if (Args.Data != null)
                        fbPackage.outputPane.OutputString(Args.Data + "\r");
                };

                FBProcess.OutputDataReceived += OutputEventHandler;
                FBProcess.Start();
                FBProcess.BeginOutputReadLine();
                //FBProcess.WaitForExit();
            }
            catch (Exception ex)
            {
                fbPackage.outputPane.OutputString("VSIX exception launching fastbuild. Could be a broken VSIX? Exception: " + ex.Message + "\r");
            }
        }
    }
}
