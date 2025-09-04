using CadEye.Lib;
using CadEye.View;
using CadEye.ViewCs;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace CadEye.ViewCS
{
    public class FileInfoItem
    {
        public long Key { get; set; }
        public string FilePath { get; set; }
    }

    public class Event_History_Author
    {
        public string Event_Messages { get; set; }
    }

    public class Bridge : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;
        public AsyncCommand Compare_Pdf_Command { get; }
        public AsyncCommand BackUp_Command { get; }
        public AsyncCommand Reset_Command { get; }
        public AsyncCommand DB_Update_Command { get; }
        public AsyncCommand Tree_Update_Command { get; }
        public AsyncCommand DB_Read_Command { get; }
        public RelayCommand File_Open_Command { get; }
        public AsyncCommand Folder_Open_Command { get; }
        public AsyncCommandT<string> Enter_Search_Command { get; }
        public AsyncCommand ViewForm_Command { get; }

        private static Bridge _instance;
        public static Bridge Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Bridge();
                }
                return _instance;
            }
        }

        public FunctionCollection _functionCollection
        {
            get { return FunctionCollection.Instance; }
        }

        public Bridge()
        {
            Compare_Pdf_Command = new AsyncCommand(WorkFlow_Pdf_Compare_Result);
            BackUp_Command = new AsyncCommand(WorkFlow_DB_Backup);
            Reset_Command = new AsyncCommand(WorkFlow_Reset);
            DB_Update_Command = new AsyncCommand(WorkFlow_DB_Update);
            Tree_Update_Command = new AsyncCommand(WorkFlow_Tree_Update);
            DB_Read_Command = new AsyncCommand(WorkFlow_DB_Read_Command);
            File_Open_Command = new RelayCommand(WorkFlow_File_Open_Command);
            Folder_Open_Command = new AsyncCommand(WorkFlow_Folder_Open_Command);
            Enter_Search_Command = new AsyncCommandT<string>(WorkFlow_Tag_Enter_Search_Command);
            ViewForm_Command = new AsyncCommand(WorkFlow_ViewForm_Command);
        }

        public async Task WorkFlow_Pdf_Compare_Result()
        {
            try
            {
                await _functionCollection.LoadPdf_Current();
            }
            catch (Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public string BackupButtonText
        {
            get => _backupButtonText;
            set
            {
                _backupButtonText = value;
                OnPropertyChanged();
            }
        }

        public async Task WorkFlow_DB_Backup()
        {
            try
            {
                BackupbtnToggle = !BackupbtnToggle;
                BackupButtonText = BackupbtnToggle ? "Progress" : "Stop";
                if (!BackupbtnToggle) return;

                string backup_folder = "";
                for (int retry = 0; retry < 3; retry++)
                {
                    backup_folder = Path.Combine(await _functionCollection.Folder_Select(), projectname);
                    if (string.IsNullOrEmpty(backup_folder))
                    {
                        System.Windows.MessageBox.Show("Select Folder", "알림");
                        continue;
                    }
                    else break;
                }
                if (string.IsNullOrEmpty(backup_folder))
                {
                    System.Windows.MessageBox.Show("Backup folder not selected", "알림");
                    return;
                }

                if (!Directory.Exists(backup_folder)) Directory.CreateDirectory(backup_folder);

                await Task.Delay(100);


                string relative_Path = AppDomain.CurrentDomain.BaseDirectory;
                sourceDbFile = System.IO.Path.Combine(relative_Path, $"{projectname}.db");
                sourceLogFile = System.IO.Path.Combine(relative_Path, $"{projectname}-log.db");

                // fire-and-forget Task
                // 반환값을 사용 하지 않고 버림.
                _ = Task.Run(async () =>
                 {
                     try
                     {
                         while (BackupbtnToggle)
                         {
                             DateTime now = DateTime.Now;
                             DateTime time = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

                             string timestamp = DateTime.Now.ToString("yyyy.MM.dd,HH.mm.ss");
                             string targetDbFile = Path.Combine(backup_folder, $"{timestamp}_{targetFileName}.db");
                             string targetLogFile = Path.Combine(backup_folder, $"{timestamp}_{targetFileName}-log.db");

                             if (!await _functionCollection.SafeCopy(sourceDbFile, targetDbFile)) continue;
                             if (!await _functionCollection.SafeCopy(sourceLogFile, targetLogFile)) continue;

                             await Task.Delay(60000);
                         }
                     }
                     catch (Exception ex)
                     {
                         _functionCollection.Event_History_Add($"Backup error: {ex}");
                     }
                 });
            }
            catch (Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public async Task WorkFlow_Reset()
        {
            try
            {
                var result = System.Windows.MessageBox.Show("정말로 초기화 하시겠습니까?", "확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    File_History.Clear();
                    await _functionCollection.Reset();
                    await _functionCollection.MainView_Start_Load();
                    await _functionCollection.File_View_input();
                }
            }
            catch (Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public async Task WorkFlow_DB_Update()
        {
            try
            {
                await Task.Run(() => _functionCollection.MainView_Start_Load());
                await _functionCollection.Exclude_FileINFO();
                await _functionCollection.File_View_input();
            }
            catch (Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public async Task WorkFlow_Tree_Update()
        {
            try
            {
                if (isinitial == 0) // hash 코드기반으로 수정 하는도중 버튼 누를시 새로생성됨 초기만 진행 
                {
                    await Task.Run(() => _functionCollection.MainView_Start_Load());
                }
                isinitial = 1;
                File_History.Clear();
                await _functionCollection.Pdf_Reset();
                await _functionCollection.File_View_input();
            }
            catch (Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public async Task WorkFlow_DB_Read_Command()
        {
            try
            {
                for (int retry = 0; retry < 10; retry++)
                {
                    if (!string.IsNullOrEmpty(user_file) && !string.IsNullOrEmpty(user_log))
                        await _functionCollection.DeleteFileSafely();

                    if (!await _functionCollection.TryPrepareTempFiles()) continue;
                    if (!await _functionCollection.SafeCopy(sourceDbFile, targetDbFile)) continue;
                    if (!await _functionCollection.SafeCopy(sourceLogFile, targetLogFile)) continue;
                    DatabaseProvider.Initialize(false, targetFileName);
                    user_file = targetDbFile;
                    user_log = targetLogFile;
                    if (await _functionCollection.Db_Read_Test())
                        break;
                }
                await _functionCollection.File_View_input();
            }
            catch (Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public void WorkFlow_File_Open_Command()
        {
            try
            {
                selected_key = SelectedFile.Key;
                _functionCollection.File_Open();
            }
            catch (Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public async Task WorkFlow_Folder_Open_Command()
        {
            try
            {
                selected_key = SelectedFile.Key;
                await _functionCollection.Folder_Open();
            }
            catch (Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public string TagText
        {
            get => _tagtext;
            set
            {
                _tagtext = value;
                OnPropertyChanged();
                PlaceHolder(_tagtext);
            }
        } // 수정

        public string FileText
        {
            get => _filetext;
            set
            {
                _filetext = value;
                OnPropertyChanged();
                PlaceHolder(_tagtext);
            }
        } // 수정

        public void PlaceHolder(string _tagtext)
        {
            if (string.IsNullOrEmpty(_tagtext))
                TagText = "tag";
        }

        public async Task WorkFlow_Tag_Enter_Search_Command(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                await _functionCollection.Tag_Enter_SearchAsync(text);
            }
            catch (Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public FileInfoItem SelectedFile
        {
            get => selectedFileinfo;
            set
            {
                selectedFileinfo = value;
                OnPropertyChanged();
                _ = OnSelectedFileChangedAsync(selectedFileinfo);
            }
        }

        private async Task OnSelectedFileChangedAsync(FileInfoItem file)
        {
            try
            {
                if (file == null) return;

                await WorkFlow_File_List_Selecetd_Command(file.Key);
            }
            catch (Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public async Task WorkFlow_File_List_Selecetd_Command(long key)
        {
            try
            {
                await _functionCollection.TagRef_Container();
                await _functionCollection.Pdf_Reset();
                await _functionCollection.LoadPdf();
                File_History.Clear();
                await _functionCollection.Data_View();
            }
            catch(Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public EventEntry Selected_EventEntry_File
        {
            get => selectedEventEntry_File;
            set
            {
                selectedEventEntry_File = value;
                OnPropertyChanged();
                _ = EventEntry_FileChangedAsync(selectedEventEntry_File);
            }
        }

        private async Task EventEntry_FileChangedAsync(EventEntry file)
        {
            try
            {
                if (file == null) return;
                await WorkFlow_EventEntry_File_Selecetd_Command(file);
            }
            catch (Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public async Task WorkFlow_EventEntry_File_Selecetd_Command(EventEntry file)
        {
            try
            {
                await _functionCollection.Pdf_Reset();
                await _functionCollection.LoadPdf_Compare(file);
            }
            catch(Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        public async Task WorkFlow_ViewForm_Command()
        {
            try
            {
                await _functionCollection.OpenPdfInNewWindow();
            }
            catch(Exception ex)
            {
                _functionCollection.Event_History_Add($"{ex.Message}\n발생 위치:\n{ex.StackTrace}");
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ==================================================================

        public WindowsFormsHost Host1 { get; set; }
        public WindowsFormsHost Host2 { get; set; }
        public WindowsFormsHost New_Host1 { get; set; }
        public WindowsFormsHost New_Host2 { get; set; }
        public PdfContainer Pdf1 { get; set; }
        public PdfContainer2 Pdf2 { get; set; }
        public string folderpath { get; set; }
        public ObservableCollection<FileInfoItem> Files { get; set; } = new ObservableCollection<FileInfoItem>();
        public ObservableCollection<string> Event_History { get; set; } = new ObservableCollection<string>();
        public string projectname { get; set; }
        public string user_db;
        public string user_file;
        public string user_log;
        public int isinitial = 0;
        public string sourceDbFile;
        public string sourceLogFile;
        public string targetDbFile;
        public string targetLogFile;
        public string targetFileName;
        public string backup_folder;
        public bool BackupbtnToggle = false;
        public string _backupButtonText = "Start Backup";
        public string _tagtext = "Tag";
        public string _filetext = "File";
        public long selected_key;
        public string repository_path { get; set; }
        public (string, bool) pathset;
        public FileInfoItem selectedFileinfo;
        public EventEntry selectedEventEntry_File;
        public byte[] Main_Image = new byte[] { };
        public ObservableCollection<string> Ref { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> Tag { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<EventEntry> File_History { get; set; } = new ObservableCollection<EventEntry>();
        public byte[] Compare_Image = new byte[] { };
    }
}
