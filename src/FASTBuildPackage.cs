using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VsFBuildHelper.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using EnvDTE;

namespace VsFBuildHelper
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("VsFBuildHelper", "Tools for CMake Fastbuild", "1.0")]
    [ProvideToolWindow(typeof(BuildAndRunWindow), Style = VsDockStyle.Tabbed, DockedWidth = 300, Window = "DocumentWell", Orientation = ToolWindowOrientation.Left)]
    [Guid("6e3b2e95-902b-4385-a966-30c06ab3c7a6")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class FASTBuildPackage : AsyncPackage
    {

        public EnvDTE80.DTE2 dte;
        public OutputWindowPane outputPane;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            await ShowToolWindow.InitializeAsync(this);
            dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;

            OutputWindow outputWindow = dte.ToolWindows.OutputWindow;

            outputPane = outputWindow.OutputWindowPanes.Add("FASTBuild");
            outputPane.OutputString("FASTBuild\r");
            FASTBuild.Initialize(this);


        }

        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            return toolWindowType.Equals(Guid.Parse(BuildAndRunWindow.WindowGuidString)) ? this : null;
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            return toolWindowType == typeof(BuildAndRunWindow) ? BuildAndRunWindow.Title : base.GetToolWindowTitle(toolWindowType, id);
        }

        protected override async Task<object> InitializeToolWindowAsync(Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            // Perform as much work as possible in this method which is being run on a background thread.
            // The object returned from this method is passed into the constructor of the SampleToolWindow 
            var dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;

            return new BuildAndRunWindowState
            {
                DTE = dte
            };
        }

        public string FBPath
        {
            get
            {
                return "C:\\tools\\fastbuild\\FBuild.exe";
            }
        }


    }
}
