using System.Windows;
using System.Windows.Controls;

namespace VsFBuildHelper.ToolWindows
{
    public partial class BuildAndRunWindowControl : UserControl
    {
        private BuildAndRunWindowState _state;

        public BuildAndRunWindowControl(BuildAndRunWindowState state)
        {
            _state = state;

            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string version = _state.DTE.FullName;

            MessageBox.Show($"Visual Studio is located here: '{version}'");
        }
    }
}
