using CadEye.Lib;
using CadEye.ViewCs;
using CadEye.ViewCS;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

namespace CadEye
{
    public partial class MainWindow : Window
    {
        private Bridge _vb { get; set; }
        private FunctionCollection _functionCollection { get; set; }
        public static bool isManger;

        public MainWindow()
        {
            InitializeComponent();
            _vb = Bridge.Instance;
            _functionCollection = FunctionCollection.Instance;
            this.DataContext = _vb;
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            Bridge.Instance.Pdf1 = Pdf_Viewer;
            Bridge.Instance.Pdf2 = Pdf_Viewer2;
        }
        public async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _functionCollection.Path_Setting();

                var result = System.Windows.MessageBox.Show(
                    "Manger Mode?",
                    "알림",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    DatabaseProvider.Initialize(true, _vb.projectname);
                    isManger = true;
                    await _functionCollection.MainView_Start_Load();
                    await _functionCollection.File_View_input();
                    _functionCollection.FolderWatcher();
                    _functionCollection.FolderWatcher_repository();
                }
                else
                {
                    isManger = false;
                    Cad_FileView1.Unvisible_btn();
                    Authority.Visibility = Visibility.Hidden;
                    Autobutton.Visibility = Visibility.Hidden;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void MainWindow_Closing(object sender, EventArgs e)
        {
            if (isManger == false)
            {
                DatabaseProvider.Dispose();

                if (_vb.user_file != null)
                {
                    File.Delete(_vb.user_file);
                    File.Delete(_vb.user_log);
                    Thread.Sleep(1000);
                }
            }
            else
            { return; }
        }
        private void Mainbutton_Click(object sender, RoutedEventArgs e)
        {
            Main.Visibility = Visibility.Visible;
            Home.Visibility = Visibility.Hidden;
            Authority.Visibility = Visibility.Hidden;
        }
        private void Homebutton_Click(object sender, RoutedEventArgs e)
        {
            Main.Visibility = Visibility.Hidden;
            Home.Visibility = Visibility.Visible;
            Authority.Visibility = Visibility.Hidden;
        }
        private void Authority_Click(object sender, RoutedEventArgs e)
        {
            Main.Visibility = Visibility.Hidden;
            Home.Visibility = Visibility.Hidden;
            Authority.Visibility = Visibility.Visible;
        }
        private NotifyIcon _trayIcon;
        private void Traybutton_Click(object sender, RoutedEventArgs e)
        {
            if (_trayIcon == null)
            {
                _trayIcon = new NotifyIcon();
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                _trayIcon.Icon = new Icon(iconPath);
                _trayIcon.Visible = true;
                _trayIcon.Text = "My WPF App";

                _trayIcon.MouseClick += (s, args) =>
                {
                    if (args.Button == MouseButtons.Left)
                    {
                        this.Show();
                        this.WindowState = WindowState.Normal;
                    }
                };

                _trayIcon.ContextMenuStrip = new ContextMenuStrip();
                _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, args) =>
                {
                    _trayIcon.Visible = false;
                    System.Windows.Application.Current.Shutdown();
                });
            }

            this.Hide();
        }
        private bool isExpanded = true;
        private void SectionExpanded(object sender, RoutedEventArgs e)
        {

            if (!isExpanded)
            {
                this.Width = 700;
                ColHistory.Width = new GridLength(0);
                ColCurrent.Width = new GridLength(1, GridUnitType.Star);
                isExpanded = true;
            }
            else
            {
                this.Width = 1400;
                ColHistory.Width = new GridLength(1, GridUnitType.Star);
                ColCurrent.Width = new GridLength(1, GridUnitType.Star);
                isExpanded = false;
            }
        }
    }
}
