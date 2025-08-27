using CadEye.Lib;
using CadEye.View;
using CadEye.ViewCS;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CadEye
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public Bridge vm { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            vm = Bridge.Instance;
            this.DataContext = vm;
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            Bridge.Instance.pdfpage = Pdf_Viewer;
            Bridge.Instance.pdfpage2 = Pdf_Viewer2;
        }

        private void Left_Click(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
            if (e.ClickCount == 2)
            {
                if (this.WindowState == WindowState.Normal)
                    this.WindowState = WindowState.Maximized;
                else if (this.WindowState == WindowState.Maximized)
                    this.WindowState = WindowState.Normal;
            }
        }
        private void Form_Closed(object sender, EventArgs e)
        {
            System.Windows.Application.Current.Shutdown(0);
        }
        private void Image_Hover(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Image img)
            {
                if (e.RoutedEvent == Mouse.MouseEnterEvent)
                    img.RenderTransform = new ScaleTransform(1.1, 1.1);
                else if (e.RoutedEvent == Mouse.MouseLeaveEvent)
                    img.RenderTransform = new ScaleTransform(1, 1);
            }
        }


        public static bool isManger;
        public Cad_FileView fileview = new Cad_FileView();
        public void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                vm.Path_Setting();

                var result = MessageBox.Show(
                    "Manger Mode?",
                    "알림",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    DatabaseProvider.Initialize(true, Bridge.projectname);
                    isManger = true;
                    fileview.FolderSearch_btn(sender, e);
                    vm.FolderWatcher();
                    vm.FolderWatcher_repository();
                }
                else
                {
                    isManger = false;
                    Cad_FileView1.Unvisible_btn();
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

                if (vm.user_file != null)
                {
                    File.Delete(vm.user_file);
                    File.Delete(vm.user_log);
                    Thread.Sleep(500);
                }
            }
        }
    }
}
