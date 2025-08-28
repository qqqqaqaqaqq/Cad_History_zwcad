using CadEye.ViewCS;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CadEye.View
{
    public partial class Cad_FileView : System.Windows.Controls.UserControl
    {
        public Bridge vm { get; set; }
        public Cad_FileView()
        {
            InitializeComponent();
            vm = Bridge.Instance;
            this.DataContext = vm;
        }
        public void Unvisible_btn()
        {
            DB_Update_btn.Visibility = Visibility.Hidden;
            DB_Reset_btm.Visibility = Visibility.Hidden;
            DB_Write_btn.Visibility = Visibility.Hidden;
            DB_Read_btn.Visibility = Visibility.Visible;
        }
        public async void File_List_Selecetd(object sender, SelectedCellsChangedEventArgs e)
        {
            if (Child_Box.SelectedItem is FileInfoItem selected)
            {
                string filenameOnly = selected.FilePath;
                vm.selectedItem = filenameOnly;
                vm.File_Description();
                await vm.Pdf_Load_btn();
                vm.Data_View();
            }
        }
        public async void FolderSearch_btn(object sender, RoutedEventArgs e)
        {
            Overlay.Visibility = Visibility.Visible;
            await vm.MainView_Start_Load_Event();
            vm.File_input_Event();
            Overlay.Visibility = Visibility.Hidden;
        }
        private async void File_Name_Enter_Search(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string query = File_SearchText.Text;
                await vm.File_Nmae_Enter_SearchAsync(query);
            }
        }
        private void Overlay_Tag(object sender, TextChangedEventArgs e)
        {
            if (Tag_SearchText.Text.Length > 0)
                PlaceHolder_Overlay_Tag.Visibility = Visibility.Hidden;
            else
                PlaceHolder_Overlay_Tag.Visibility = Visibility.Visible;
        }
        private void Overlay_FileName(object sender, TextChangedEventArgs e)
        {
            if (File_SearchText.Text.Length > 0)
                PlaceHolder_Overlay_FileName.Visibility = Visibility.Hidden;
            else
                PlaceHolder_Overlay_FileName.Visibility = Visibility.Visible;
        }
        private async void Tag_Enter_Search(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string query = Tag_SearchText.Text;
                await vm.Tag_Enter_SearchAsync(query);
            }
        }
        private void Open_File(object sender, RoutedEventArgs e)
        {
            if (Child_Box.SelectedItem is FileInfoItem item)
            {
                vm.selectedItem = item.FilePath; // 또는 item.FullPath 등 원하는 속성
                vm.Open_File();
            }
        }
        private void Open_Folder(object sender, RoutedEventArgs e)
        {
            if (Child_Box.SelectedItem is FileInfoItem item)
            {
                vm.selectedItem = item.FilePath;
                vm.Open_Folder();
            }
        }
        private async void DB_Update(object sender, RoutedEventArgs e)
        {
            Overlay.Visibility = Visibility.Visible;
            await Task.Run(() => vm.MainView_Start_Load());
            vm.File_input_Event();
            await vm.Extrude_btn();
            await vm.Pdf_Bitmap_btn();
            Overlay.Visibility = Visibility.Hidden;
        }
        public async void Tree_Update(object sender, RoutedEventArgs e)
        {
            Overlay.Visibility = Visibility.Visible;
            await Task.Run(() => vm.MainView_Start_Load());
            vm.File_input_Event();
            Overlay.Visibility = Visibility.Hidden;
        }
        public void DB_Read(object sender, RoutedEventArgs e)
        {
            Overlay.Visibility = Visibility.Visible;
            vm.File_input_Event();
            Overlay.Visibility = Visibility.Hidden;
        }
        private async void Reset(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("정말로 초기화 하시겠습니까?", "확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Overlay.Visibility = Visibility.Visible;
                await Task.Run(() => vm.Reset());
                await Task.Run(() => vm.MainView_Start_Load());
                vm.File_input_Event();
                Overlay.Visibility = Visibility.Hidden;
            }
        }
    }
}