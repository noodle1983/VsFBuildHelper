using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using EnvDTE;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using Newtonsoft.Json;

namespace VsFBuildHelper.ToolWindows
{
    [Serializable]
    public class DebugInstanceInfo
    {
        [NonSerialized]
        public string group = "default";
        public string projectName;
        public string cmdParam;
        public string cmdDir;
        public int delay = 0;
    }

    public class DebugInstance
    {
        public DebugInstanceInfo info;
        public DateTime exeTime;
    }



    [Serializable]
    public struct ConfigStruct
    {
        public Dictionary<string, List<DebugInstanceInfo>> allDebugInstance;
    }

    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid(WindowGuidString)]
    public class BuildAndRunWindow : ToolWindowPane
    {
        public const string WindowGuidString = "e4e2ba26-a455-4c53-adb3-8225fb696f8b"; // Replace with new GUID in your own code
        public const string Title = "Build And Run Window";

        // "state" parameter is the object returned from MyPackage.InitializeToolWindowAsync
        public BuildAndRunWindow(BuildAndRunWindowState state) : base()
        {
            this.Caption = "FastBuildAndRunWindow";

            if (dispatcherTimer == null)
            {
                dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
                dispatcherTimer.Interval = TimeSpan.FromSeconds(1);
                dispatcherTimer.Tick += OnTick;
                dispatcherTimer.Start();
            }

            GetDebugInstanceConfig();

            // Add Layout control
            topStackPanel = new StackPanel();
            this.Content = topStackPanel;

            topStackPanel.Orientation = Orientation.Vertical;
            topStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
            topStackPanel.VerticalAlignment = VerticalAlignment.Top;
            Caption = Title;
            BitmapImageMoniker = KnownMonikers.ImageIcon;
        }

        public static Dictionary<string, List<DebugInstanceInfo>> allDebugInstance = new Dictionary<string, List<DebugInstanceInfo>>();
        public static DebugInstanceInfo curDebugInstance = null;
        static List<DebugInstance> waitingQueue = new List<DebugInstance>();

        System.Windows.Threading.DispatcherTimer dispatcherTimer;
        StackPanel topStackPanel;
        TextBox sharedIpTextBlock;
        Button buildAllButton;

        TextBox groupValueBlock;
        ComboBox projComboBox;
        List<string> projComboBoxItems = new List<string>();
        TextBox runParamValueBlock;
        TextBox dirValueBlock;
        TextBox delayValueBlock;

        public static bool IsFastbuildInstalled()
        {
            string fbuildPath = "C:\\tools\\fastbuild\\FBuild.exe";
            return File.Exists(fbuildPath);
        }

        public static string GetSlnFullPathName()
        {
            var package = FASTBuild.Instance.Package;
            if (null == package || null == package.dte.Solution)
            {
                return string.Empty;
            }
            return package.dte.Solution.FullName;
        }

        public static string GetSlnName()
        {
            var fullName = GetSlnFullPathName();
            if (string.IsNullOrEmpty(fullName)) { return string.Empty; }
            return Path.GetFileNameWithoutExtension(fullName);
        }

        public static Dictionary<string, Project> GetAllVcProject()
        {
            var allVcProject = new Dictionary<string, Project>();
            var package = FASTBuild.Instance.Package;
            if (null == package || null == package.dte.Solution)
            {
                return allVcProject;
            }
            var sln = package.dte.Solution;

            var projects = sln.Projects;
            foreach (Project proj in projects)
            {
                if (proj == null) { continue; }
                var vcproj = proj.Object as VCProject;
                if (vcproj == null) { continue; }
                if (vcproj.ActiveConfiguration.ConfigurationType == ConfigurationTypes.typeApplication)
                {
                    allVcProject[vcproj.Name] = proj;
                }

            }

            return allVcProject;

        }

        public static string GetConfigPath()
        {
            var package = FASTBuild.Instance.Package;
            if (null == package || null == package.dte.Solution || !package.dte.Solution.IsOpen)
            {
                return string.Empty;
            }

            string slnPath = package.dte.Solution.FullName;

            string dir = Path.GetDirectoryName(slnPath);
            string slnName = Path.GetFileNameWithoutExtension(slnPath);
            return dir + "\\" + slnName + ".debuginstance.json";

        }

        public static void WriteCofnigFile()
        {
            var configPath = GetConfigPath();
            ConfigStruct config;
            config.allDebugInstance = allDebugInstance;
            string fileContent = JsonConvert.SerializeObject(config);
            File.WriteAllText(configPath, fileContent);
        }

        public Dictionary<string, List<DebugInstanceInfo>> GetDebugInstanceConfig()
        {
            var configPath = GetConfigPath();
            if (!File.Exists(configPath))
            {
                allDebugInstance = new Dictionary<string, List<DebugInstanceInfo>>();
                var defaultInstance = new List<DebugInstanceInfo>();
                var projects = GetAllVcProject();
                if (projects.Count == 0) { return allDebugInstance; }
                foreach (var proj in projects)
                {
                    DebugInstanceInfo info = new DebugInstanceInfo();
                    info.projectName = proj.Key;
                    info.cmdDir = "${TargetDir}";
                    defaultInstance.Add(info);
                }
                allDebugInstance["default"] = defaultInstance;

                WriteCofnigFile();
                return allDebugInstance;
            }

            string content = File.ReadAllText(configPath);
            var configStruct = JsonConvert.DeserializeObject<ConfigStruct>(content);
            allDebugInstance = configStruct.allDebugInstance;
            return allDebugInstance;
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="BuildAndRunWindow"/> class.
        /// </summary>
        //<grid>
        //    <stackpanel orientation="vertical">
        //        <stackpanel orientation="horizontal">
        //            <textblock width="200" background="white" height ="40" text="192.168.0.1" textalignment="center" fontsize="30" />
        //            <button content="installfastbuild" click="button1_click" width="90" height="40" x:name="installbtn" margin="10,0"/>
        //        </stackpanel>
        //        <stackpanel orientation="horizontal">
        //            <button content="build all" click="button1_click" width="90" height="40" x:name="buildallbtn" margin="0"/>
        //            <button content="clean" click="button1_click" width="90" height="40" x:name="cleanallbtn" margin="10"/>
        //            <button content="run all" click="button1_click" width="90" height="40" x:name="runallbtn" margin="10"/>
        //        </stackpanel>
        //        <scrollviewer>
        //            <stackpanel orientation="horizontal">
        //                <textblock margin="10" width="150" horizontalalignment="left" verticalalignment="center">projectname</textblock>
        //                <button content="run" click="button1_click" width="30" height="30" x:name="runbtn" margin="10"/>
        //                <button content="del" click="button1_click" width="30" height="30" x:name="delbtn" margin="10"/>
        //            </stackpanel>

        //        </scrollviewer>
        //        <textblock margin="10" horizontalalignment="center">buildandrunwindow</textblock>
        //    </stackpanel>
        //</grid>

        protected override void OnCreate()
        {
            base.OnCreate();
            CreateContent();
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (buildAllButton != null)
            {
                buildAllButton.Content = FASTBuild.IsFbProcessRunning() ? "Break" : "BuildAll";
            }

            bool hasDebugInstanceToStart = waitingQueue.Count > 0;
            if (hasDebugInstanceToStart)
            {
                var now = DateTime.Now;
                waitingQueue.Sort((a, b) => a.exeTime.CompareTo(b.exeTime));
                for (int i = waitingQueue.Count - 1; i >= 0; i--)
                {
                    var inst = waitingQueue[i];
                    if (inst.exeTime <= now)
                    {
                        try
                        {
                            StartDebugInstance(inst.info);
                            waitingQueue.RemoveAt(i);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

            }
        }

        private void CreateContent()
        {
            topStackPanel.Children.Clear();

            {
                var installStackPanel = new StackPanel();
                installStackPanel.Orientation = Orientation.Horizontal;
                installStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
                installStackPanel.VerticalAlignment = VerticalAlignment.Top;
                topStackPanel.Children.Add(installStackPanel);

                sharedIpTextBlock = new TextBox();
                sharedIpTextBlock.Margin = new Thickness(10, 0, 0, 0);
                sharedIpTextBlock.Width = 190;
                sharedIpTextBlock.Height = 40;
                sharedIpTextBlock.FontSize = 30;
                sharedIpTextBlock.TextAlignment = TextAlignment.Center;
                sharedIpTextBlock.Text = "\\\\192.168.0.1";
                installStackPanel.Children.Add(sharedIpTextBlock);

                var installButton = new Button();
                installButton.Width = 90;
                installButton.Height = 40;
                installButton.Margin = new Thickness(10, 0, 0, 0);
                installButton.Content = IsFastbuildInstalled() ? "ReinstallFastbuild" : "InstallFastbuild";
                installButton.Name = "installbtn";
                installButton.Click += new RoutedEventHandler(InstallFastbuild);
                installStackPanel.Children.Add(installButton);
            }
            //if (!IsFastbuildInstalled()) { return; }

            {
                var globalBtnStackPanel = new StackPanel();
                globalBtnStackPanel.Orientation = Orientation.Horizontal;
                globalBtnStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
                globalBtnStackPanel.VerticalAlignment = VerticalAlignment.Top;
                topStackPanel.Children.Add(globalBtnStackPanel);

                buildAllButton = new Button();
                buildAllButton.Width = 90;
                buildAllButton.Height = 40;
                buildAllButton.Margin = new Thickness(10, 20, 0, 10);
                buildAllButton.Content = "BuildAll";
                buildAllButton.Name = "buildallbtn";
                buildAllButton.Click += new RoutedEventHandler(BuildAll);
                globalBtnStackPanel.Children.Add(buildAllButton);

                var cleanAllButton = new Button();
                cleanAllButton.Width = 90;
                cleanAllButton.Height = 40;
                cleanAllButton.Margin = new Thickness(10, 20, 0, 10);
                cleanAllButton.Content = "Clean";
                cleanAllButton.Name = "cleanallbtn";
                cleanAllButton.Click += new RoutedEventHandler(CleanAll);
                globalBtnStackPanel.Children.Add(cleanAllButton);

                var runAllButton = new Button();
                runAllButton.Width = 90;
                runAllButton.Height = 40;
                runAllButton.Margin = new Thickness(10, 20, 0, 10);
                runAllButton.Content = "RunAll";
                runAllButton.Name = "runallbtn";
                runAllButton.Click += new RoutedEventHandler(RunAll);
                globalBtnStackPanel.Children.Add(runAllButton);
            }


            {
                // Define a ScrollViewer
                var scrollViewStackPanel = new StackPanel();
                scrollViewStackPanel.Orientation = Orientation.Vertical;
                scrollViewStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
                scrollViewStackPanel.VerticalAlignment = VerticalAlignment.Top;

                var scrollViewer = new ScrollViewer();
                scrollViewer.Margin = new Thickness(10, 0, 0, 10);
                scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollViewer.Content = scrollViewStackPanel;
                topStackPanel.Children.Add(scrollViewer);

                foreach (var configPair in allDebugInstance)
                {
                    var groupName = configPair.Key;
                    if (configPair.Value.Count > 1)
                    {
                        var groupStackPanel = new StackPanel();
                        groupStackPanel.Margin = new Thickness(0, 5, 0, 0);
                        groupStackPanel.Orientation = Orientation.Horizontal;
                        groupStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
                        groupStackPanel.VerticalAlignment = VerticalAlignment.Top;
                        scrollViewStackPanel.Children.Add(groupStackPanel);

                        var groupNameTextBlock = new TextBlock();
                        groupNameTextBlock.Margin = new Thickness(0, 0, 0, 0);
                        groupNameTextBlock.Width = 200;
                        groupNameTextBlock.Height = 30;
                        //projectNameTextBlock.FontSize = 0;
                        groupNameTextBlock.TextAlignment = TextAlignment.Left;
                        groupNameTextBlock.VerticalAlignment = VerticalAlignment.Center;
                        groupNameTextBlock.Text = groupName; ;
                        groupStackPanel.Children.Add(groupNameTextBlock);

                        var runGroupButton = new Button();
                        runGroupButton.Width = 90;
                        runGroupButton.Height = 30;
                        runGroupButton.Margin = new Thickness(10, 0, 0, 0);
                        runGroupButton.Content = "RunGroup";
                        runGroupButton.Name = "rungroupbtn";
                        runGroupButton.Click += new RoutedEventHandler(runGroupProject);
                        runGroupButton.DataContext = configPair.Value;
                        groupStackPanel.Children.Add(runGroupButton);
                    }

                    foreach (var instanceConfig in configPair.Value)
                    {
                        var projStackPanel = new StackPanel();
                        projStackPanel.Orientation = Orientation.Horizontal;
                        projStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
                        projStackPanel.VerticalAlignment = VerticalAlignment.Top;
                        var panelBtn = new Button();
                        panelBtn.Content = projStackPanel;
                        panelBtn.Margin = new Thickness(0, 5, 0, 0);
                        //panelBtn.Click += new RoutedEventHandler(showProjParam);
                        panelBtn.DataContext = instanceConfig;
                        scrollViewStackPanel.Children.Add(panelBtn);

                        var projectNameTextBlock = new TextBlock();
                        projectNameTextBlock.Margin = new Thickness(10, 0, 0, 0);
                        projectNameTextBlock.Width = 175 + 50;
                        projectNameTextBlock.Height = 30;
                        //projectNameTextBlock.FontSize = 0;
                        projectNameTextBlock.TextAlignment = TextAlignment.Center;
                        projectNameTextBlock.VerticalAlignment = VerticalAlignment.Center;
                        projectNameTextBlock.Text = string.Format("{0}-{1}-{2}\n{3}", instanceConfig.delay, instanceConfig.projectName, instanceConfig.cmdParam, instanceConfig.cmdDir);
                        projStackPanel.Children.Add(projectNameTextBlock);

                        var runButton = new Button();
                        runButton.Width = 40;
                        runButton.Height = 30;
                        runButton.Margin = new Thickness(10, 0, 0, 0);
                        runButton.Content = "Run";
                        runButton.Name = "runbtn";
                        runButton.Click += new RoutedEventHandler(runProject);
                        runButton.DataContext = instanceConfig;
                        projStackPanel.Children.Add(runButton);

                        //var delButton = new Button();
                        //delButton.Width = 40;
                        //delButton.Height = 30;
                        //delButton.Margin = new Thickness(10, 0, 0, 0);
                        //delButton.Content = "Del";
                        //delButton.Name = "delbtn";
                        //delButton.Click += new RoutedEventHandler(delProject);
                        //delButton.DataContext = instanceConfig;
                        //projStackPanel.Children.Add(delButton);
                    }
                }
            }

            ////group
            //{
            //    var groupStackPanel = new StackPanel();
            //    groupStackPanel.Orientation = Orientation.Horizontal;
            //    groupStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
            //    groupStackPanel.VerticalAlignment = VerticalAlignment.Top;
            //    topStackPanel.Children.Add(groupStackPanel);

            //    var groupLabelBlock = new TextBlock();
            //    groupLabelBlock.TextWrapping = TextWrapping.Wrap;
            //    groupLabelBlock.Margin = new Thickness(10, 0, 0, 10);
            //    groupLabelBlock.Height = 20;
            //    groupLabelBlock.Text = "group:";
            //    groupLabelBlock.Width = 80;
            //    groupLabelBlock.VerticalAlignment = VerticalAlignment.Center;
            //    groupLabelBlock.TextAlignment = TextAlignment.Center;
            //    groupStackPanel.Children.Add(groupLabelBlock);

            //    groupValueBlock = new TextBox();
            //    groupValueBlock.Margin = new Thickness(10, 0, 0, 10);
            //    groupValueBlock.Width = 200;
            //    groupValueBlock.Height = 20;
            //    groupValueBlock.TextAlignment = TextAlignment.Center;
            //    groupValueBlock.Text = curDebugInstance == null ? "default" : curDebugInstance.group;
            //    groupStackPanel.Children.Add(groupValueBlock);
            //}

            ////project
            //{
            //    var projStackPanel = new StackPanel();
            //    projStackPanel.Orientation = Orientation.Horizontal;
            //    projStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
            //    projStackPanel.VerticalAlignment = VerticalAlignment.Top;
            //    topStackPanel.Children.Add(projStackPanel);

            //    TextBlock projLabelBlock = new TextBlock();
            //    projLabelBlock.TextWrapping = TextWrapping.Wrap;
            //    projLabelBlock.Margin = new Thickness(10, 0, 0, 10);
            //    projLabelBlock.Height = 20;
            //    projLabelBlock.Text = "project:";
            //    projLabelBlock.Width = 80;
            //    projLabelBlock.VerticalAlignment = VerticalAlignment.Center;
            //    projLabelBlock.TextAlignment = TextAlignment.Center;
            //    projStackPanel.Children.Add(projLabelBlock);

            //    projComboBox = new ComboBox();
            //    projComboBox.IsEditable = false;
            //    projComboBox.IsReadOnly = false;
            //    projComboBox.Margin = new Thickness(10, 0, 0, 10);
            //    projComboBox.Width = 200;
            //    projComboBox.Height = 20;
            //    projComboBoxItems.Clear();
            //    var projs = GetAllVcProject();
            //    foreach (var proj in projs)
            //    {
            //        var projNameText = new TextBlock();
            //        projNameText.Text = proj.Key;
            //        projComboBox.Items.Add(projNameText);
            //        projComboBoxItems.Add(proj.Key);
            //    }
            //    if (curDebugInstance == null)
            //    {
            //        projComboBox.SelectedIndex = 0;
            //    }
            //    else
            //    {
            //        for (int i = 0; i < projComboBoxItems.Count; i++) { if (projComboBoxItems[i] == curDebugInstance.projectName) { projComboBox.SelectedIndex = i; } }
            //    }
            //    projStackPanel.Children.Add(projComboBox);
            //}

            ////cmd args
            //{
            //    var runParamStackPanel = new StackPanel();
            //    runParamStackPanel.Orientation = Orientation.Horizontal;
            //    runParamStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
            //    runParamStackPanel.VerticalAlignment = VerticalAlignment.Top;
            //    topStackPanel.Children.Add(runParamStackPanel);

            //    TextBlock runParamLabelBlock = new TextBlock();
            //    runParamLabelBlock.TextWrapping = TextWrapping.Wrap;
            //    runParamLabelBlock.Margin = new Thickness(10, 0, 0, 10);
            //    runParamLabelBlock.Width = 80;
            //    runParamLabelBlock.Height = 20;
            //    runParamLabelBlock.Text = "parameter:";
            //    runParamLabelBlock.TextAlignment = TextAlignment.Left;
            //    runParamLabelBlock.VerticalAlignment = VerticalAlignment.Center;
            //    runParamStackPanel.Children.Add(runParamLabelBlock);

            //    runParamValueBlock = new TextBox();
            //    runParamValueBlock.Margin = new Thickness(10, 0, 0, 10);
            //    runParamValueBlock.Width = 200;
            //    runParamValueBlock.Height = 20;
            //    runParamValueBlock.TextAlignment = TextAlignment.Left;
            //    runParamValueBlock.Text = curDebugInstance == null? "" : curDebugInstance.cmdParam ;
            //    runParamStackPanel.Children.Add(runParamValueBlock);
            //}

            ////dir
            //{
            //    var runDirStackPanel = new StackPanel();
            //    runDirStackPanel.Orientation = Orientation.Horizontal;
            //    runDirStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
            //    runDirStackPanel.VerticalAlignment = VerticalAlignment.Top;
            //    topStackPanel.Children.Add(runDirStackPanel);

            //    TextBlock runDirLabelBlock = new TextBlock();
            //    runDirLabelBlock.TextWrapping = TextWrapping.Wrap;
            //    runDirLabelBlock.Margin = new Thickness(10, 0, 0, 10);
            //    runDirLabelBlock.Height = 20;
            //    runDirLabelBlock.Width = 80;
            //    runDirLabelBlock.Text = "directory:";
            //    runDirLabelBlock.TextAlignment = TextAlignment.Left;
            //    runDirLabelBlock.VerticalAlignment = VerticalAlignment.Center;
            //    runDirStackPanel.Children.Add(runDirLabelBlock);

            //    dirValueBlock = new TextBox();
            //    dirValueBlock.Margin = new Thickness(10, 0, 0, 10);
            //    dirValueBlock.Width = 200;
            //    dirValueBlock.Height = 20;
            //    dirValueBlock.TextAlignment = TextAlignment.Left;
            //    dirValueBlock.Text = curDebugInstance == null? "${TargetDir}" : curDebugInstance.cmdDir;
            //    runDirStackPanel.Children.Add(dirValueBlock);
            //}

            ////delay
            //{
            //    var delayStackPanel = new StackPanel();
            //    delayStackPanel.Orientation = Orientation.Horizontal;
            //    delayStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
            //    delayStackPanel.VerticalAlignment = VerticalAlignment.Top;
            //    topStackPanel.Children.Add(delayStackPanel);

            //    TextBlock delayLabelBlock = new TextBlock();
            //    delayLabelBlock.TextWrapping = TextWrapping.Wrap;
            //    delayLabelBlock.Margin = new Thickness(10, 0, 0, 10);
            //    delayLabelBlock.Height = 20;
            //    delayLabelBlock.Width = 80;
            //    delayLabelBlock.Text = "delay:";
            //    delayLabelBlock.TextAlignment = TextAlignment.Left;
            //    delayLabelBlock.VerticalAlignment = VerticalAlignment.Center;
            //    delayStackPanel.Children.Add(delayLabelBlock);

            //    delayValueBlock = new TextBox();
            //    delayValueBlock.Margin = new Thickness(10, 0, 0, 10);
            //    delayValueBlock.Width = 200;
            //    delayValueBlock.Height = 20;
            //    delayValueBlock.TextAlignment = TextAlignment.Left;
            //    delayValueBlock.Text = curDebugInstance == null? "0" : curDebugInstance.delay.ToString();
            //    delayStackPanel.Children.Add(delayValueBlock);
            //}


            //{
            //    var btnStackPanel = new StackPanel();
            //    btnStackPanel.Orientation = Orientation.Horizontal;
            //    btnStackPanel.HorizontalAlignment = HorizontalAlignment.Left;
            //    btnStackPanel.VerticalAlignment = VerticalAlignment.Top;
            //    topStackPanel.Children.Add(btnStackPanel);

            //    var editButton = new Button();
            //    editButton.Width = 90;
            //    editButton.Height = 20;
            //    editButton.Margin = new Thickness(10, 0, 0, 10);
            //    editButton.Content = "Save";
            //    editButton.Name = "editobtn";
            //    editButton.Click += new RoutedEventHandler(saveCurProject);
            //    btnStackPanel.Children.Add(editButton);

            //    var addnewButton = new Button();
            //    addnewButton.Width = 120;
            //    addnewButton.Height = 20;
            //    addnewButton.Margin = new Thickness(10, 0, 0, 10);
            //    addnewButton.Content = "New Instance";
            //    addnewButton.Name = "addnewobtn";
            //    addnewButton.Click += new RoutedEventHandler(newRunProject);
            //    btnStackPanel.Children.Add(addnewButton);

            //}

            // Add the StackPanel as the lone child of the ScrollViewer
            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            topStackPanel.UpdateLayout();
            topStackPanel.InvalidateMeasure();
            topStackPanel.InvalidateVisual();

        }

        private void runGroupProject(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) { return; }

            var instances = btn.DataContext as List<DebugInstanceInfo>;
            foreach (var debugInstance in instances)
            {
                var inst = new DebugInstance();
                inst.info = debugInstance;
                inst.exeTime = DateTime.Now;
                inst.exeTime = inst.exeTime.AddSeconds(debugInstance.delay);
                waitingQueue.Add(inst);
            }

        }

        private void InstallFastbuild(object sender, RoutedEventArgs e)
        {
            var package = FASTBuild.Instance.Package;
            if (null == package || null == package.dte.Solution || sharedIpTextBlock == null)
            {
                return;
            }

            string fastbuildPath = Assembly.GetAssembly(typeof(FASTBuild)).Location;
            string dir = Path.GetDirectoryName(fastbuildPath);
            string batFilePath = dir + "\\FastbuildMedia\\admin_install.bat";

            string ps1FilePath = dir + "\\FastbuildMedia\\install.ps1";
            string content = File.ReadAllText(ps1FilePath);
            content = content.Replace("FASTBUILD_SHARED_PATH", sharedIpTextBlock.Text);
            File.WriteAllText(ps1FilePath, content);

            try
            {
                package.outputPane.OutputString("install with script: " + batFilePath + " \r");

                System.Diagnostics.Process FBProcess = new System.Diagnostics.Process();
                FBProcess.StartInfo.FileName = "powershell";
                FBProcess.StartInfo.Arguments = string.Format("Start-Process '{0}' -verb runAs", batFilePath);
                FBProcess.StartInfo.RedirectStandardOutput = true;
                FBProcess.StartInfo.UseShellExecute = false;
                FBProcess.StartInfo.CreateNoWindow = true;
                var SystemEncoding = System.Globalization.CultureInfo.GetCultureInfo(FASTBuild.GetSystemDefaultLCID()).TextInfo.OEMCodePage;
                FBProcess.StartInfo.StandardOutputEncoding = System.Text.Encoding.GetEncoding(SystemEncoding);

                System.Diagnostics.DataReceivedEventHandler OutputEventHandler = (Sender, Args) => {
                    if (Args.Data != null)
                        package.outputPane.OutputString(Args.Data + "\r");
                };

                FBProcess.OutputDataReceived += OutputEventHandler;
                FBProcess.Start();
                FBProcess.BeginOutputReadLine();
                FBProcess.WaitForExit();

                for (int i = 0; i < 10; i++)
                {
                    if (!IsFastbuildInstalled())
                    {
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                    {
                        break;
                    }

                }

                CreateContent();
            }
            catch (Exception ex)
            {
                package.outputPane.OutputString("VSIX exception launching fastbuild. Could be a broken VSIX? Exception: " + ex.Message + "\r");
            }

        }

        private void newRunProject(object sender, RoutedEventArgs e)
        {
            var groupText = groupValueBlock.Text;
            var project = projComboBoxItems[projComboBox.SelectedIndex];
            var runParam = runParamValueBlock.Text;
            var dir = dirValueBlock.Text;
            var delay = 0;
            int.TryParse(delayValueBlock.Text, out delay);

            if (string.IsNullOrEmpty(project)) { return; }
            if (string.IsNullOrEmpty(groupText)) { groupText = "default"; }
            var instance = new DebugInstanceInfo();
            instance.group = groupText;
            instance.projectName = project;
            instance.cmdParam = runParam;
            instance.cmdDir = dir;
            instance.delay = delay;

            //insert
            List<DebugInstanceInfo> debugInstanceList;
            if (allDebugInstance.ContainsKey(groupText))
            {
                debugInstanceList = allDebugInstance[groupText];
            }
            else
            {
                debugInstanceList = new List<DebugInstanceInfo>();
                allDebugInstance[groupText] = debugInstanceList;
            }
            debugInstanceList.Add(instance);
            curDebugInstance = instance;

            WriteCofnigFile();
            CreateContent();
        }

        private void showProjParam(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) { return; }

            var instance = btn.DataContext as DebugInstanceInfo;
            if (curDebugInstance == instance) { return; }

            curDebugInstance = instance;

            groupValueBlock.Text = curDebugInstance.group;
            for (int i = 0; i < projComboBoxItems.Count; i++) { if (projComboBoxItems[i] as string == curDebugInstance.projectName) { projComboBox.SelectedIndex = i; } }
            runParamValueBlock.Text = curDebugInstance.cmdParam;
            dirValueBlock.Text = curDebugInstance.cmdDir;
            delayValueBlock.Text = curDebugInstance.delay.ToString();
        }

        private void delProject(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) { return; }

            var instance = btn.DataContext as DebugInstanceInfo;
            DeleteInstance(instance);

            WriteCofnigFile();
            CreateContent();
        }

        private static void DeleteInstance(DebugInstanceInfo instance)
        {
            foreach (var groupInstancesPair in allDebugInstance)
            {
                var instances = groupInstancesPair.Value;
                instances.Remove(instance);

            }
        }

        private void runProject(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) { return; }

            var debugInstance = btn.DataContext as DebugInstanceInfo;
            var inst = new DebugInstance();
            inst.info = debugInstance;
            inst.exeTime = DateTime.Now;
            inst.exeTime = inst.exeTime.AddSeconds(debugInstance.delay);
            waitingQueue.Add(inst);
        }

        private void RunAll(object sender, RoutedEventArgs e)
        {
            foreach (var groupInstancesPair in allDebugInstance)
            {
                var instances = groupInstancesPair.Value;
                foreach (var debugInstance in instances)
                {
                    var inst = new DebugInstance();
                    inst.info = debugInstance;
                    inst.exeTime = DateTime.Now;
                    inst.exeTime = inst.exeTime.AddSeconds(debugInstance.delay);
                    waitingQueue.Add(inst);
                }

            }
        }

        private void StartDebugInstance(DebugInstanceInfo debugInstance)
        {
            // start each instance
            FASTBuildPackage fbPackage = (FASTBuildPackage)FASTBuild.Instance.Package;
            if (null == fbPackage.dte.Solution)
                return;

            var dte = FASTBuild.Instance.Package.dte;
            var projects = GetAllVcProject();

            if (!projects.ContainsKey(debugInstance.projectName)) { return; }

            var proj = projects[debugInstance.projectName];
            var vcproj = proj.Object as VCProject;
            if (vcproj == null) { return; }

            string projectName = GetSlnName() + "\\" + debugInstance.projectName;
            dte.ToolWindows.SolutionExplorer.GetItem(projectName).Select(vsUISelectionType.vsUISelectionTypeSelect);
            var debugSettings = vcproj.ActiveConfiguration.DebugSettings as VCDebugSettings;

            var StartArgs = debugSettings.CommandArguments;
            var StartDir = debugSettings.WorkingDirectory;

            debugSettings.CommandArguments = (string.IsNullOrEmpty(debugInstance.cmdParam)) ? "" : debugInstance.cmdParam;
            debugSettings.WorkingDirectory = (string.IsNullOrEmpty(debugInstance.cmdDir)) ? "" : debugInstance.cmdDir;

            dte.ExecuteCommand("ClassViewContextMenus.ClassViewProject.Debug.Startnewinstance");

            System.Threading.Thread.Sleep(2000);
            //debugSettings.CommandArguments = StartArgs;
            //debugSettings.WorkingDirectory = StartDir;

        }

        private void CleanAll(object sender, RoutedEventArgs e)
        {
            var dte = FASTBuild.Instance.Package.dte;
            dte.ToolWindows.SolutionExplorer.GetItem(GetSlnName()).Select(vsUISelectionType.vsUISelectionTypeSelect);
            dte.ExecuteCommand("Build.CleanSolution");
        }

        private void BuildAll(object sender, RoutedEventArgs e)
        {
            if (FASTBuild.IsFbProcessRunning())
            {
                FASTBuild.StopFbProcess();
            }
            else
            {
                FASTBuild.RunfastbuildOnSln();
            }
        }

        private void saveCurProject(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) { return; }

            if (curDebugInstance == null)
            {
                newRunProject(sender, e);
                return;
            }

            var groupText = groupValueBlock.Text;
            var project = projComboBoxItems[projComboBox.SelectedIndex];
            var runParam = runParamValueBlock.Text;
            var dir = dirValueBlock.Text;

            if (string.IsNullOrEmpty(project)) { return; }
            if (string.IsNullOrEmpty(groupText)) { groupText = "default"; }

            DeleteInstance(curDebugInstance);
            newRunProject(sender, e);
        }
    }

}
