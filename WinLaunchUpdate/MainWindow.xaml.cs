using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WinLaunchUpdate
{
    public partial class MainWindow : Window
    {
        #region Hide Close Button
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private void HideCloseButton()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        }
        #endregion

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Updater.Progress += Updater_Progress;
        }

        private void Updater_Progress(object? sender, ProgressEventArgs e)
        {
            tbStatus.Text = e.Status;
            pbProgress.Value = e.Progress;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            HideCloseButton();

            Updater.Update();
        }
    }
}
