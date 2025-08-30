using CadEye.Lib;
using CadEye.View;
using LiteDB;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;

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
    public class Bridge
    {
        File_Watcher watcher = new File_Watcher();
        public FileSystemWatcher _watcher;
        public FileSystemWatcher _watcher2;
        private static Bridge _instance;
        public Data_base _db = new Data_base();
        public ZwCad_Lib cad = new ZwCad_Lib();
        public string user_db;
        public string user_file;
        public string user_log;
        public PdfContainer pdfpage;
        public PdfContainer2 pdfpage2;
        public WindowsFormsHost host = new WindowsFormsHost();
        public WindowsFormsHost host2 = new WindowsFormsHost();
        public WindowsFormsHost new_host = new WindowsFormsHost();
        public WindowsFormsHost new_host2 = new WindowsFormsHost();
        public byte[] Main_Image = new byte[] { };
        public byte[] Compare_Image = new byte[] { };
        public ObservableCollection<FileInfoItem> Files { get; set; } = new ObservableCollection<FileInfoItem>();
        public ObservableCollection<string> Ref { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> Tag { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<EventEntry> File_History { get; set; } = new ObservableCollection<EventEntry>();
        public ObservableCollection<string> Event_History { get; set; } = new ObservableCollection<string>();
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
        public File_Check file_check = new File_Check();

        // ========================================= //
        public string selectedItem { get; set; }
        public string folderpath { get; set; }
        private string repository_path { get; set; }
        public static string projectname { get; set; }
        public readonly object _Lock = new object();
        public void MainView_Start_Load()
        {
            Lib.File_Check filecheck = new Lib.File_Check();
            ConcurrentBag<Child_File> item_insert = filecheck.AllocateData(folderpath);
            _db.Child_File_Table(null, item_insert, DbAction.AllUpsert);
        }
        public void Path_Setting()
        {
            Lib.Open_folderdialog folderoepn = new Lib.Open_folderdialog();
            (string, bool) path = folderoepn.PathSetting();
            folderpath = path.Item1;
            projectname = System.IO.Path.GetFileName(folderpath);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            repository_path = System.IO.Path.Combine(baseDir, $"repository");

            if (!Directory.Exists(repository_path))
            {
                Directory.CreateDirectory(repository_path);
            }
            pathset = path;
        }
        public (string, bool) pathset;
        public async Task MainView_Start_Load_Event()
        {
            try
            {
                (string, bool) path = pathset;
                if (path.Item2 == true)
                {
                    await Task.Run(() => MainView_Start_Load());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainView_Start_Load_Event : {ex.Message}");
            }
        }
        private async Task File_input()
        {
            try
            {
                if (!MainWindow.isManger)
                {
                    bool check = ReadMode();
                    if (!check) return;
                }

                var fileInfos = await Task.Run(() =>
                {
                    try
                    {
                        var child_db = DatabaseProvider.Child_Node;
                        var child_nodes = child_db.FindAll();

                        return child_nodes
                            .Select(node => new FileInfoItem
                            {
                                Key = node.Key,
                                FilePath = node.File_FullName
                            })
                            .ToList();
                    }
                    catch (LiteDB.LiteException ex)
                    {
                        Debug.WriteLine($"[FILE_INPUT ERROR] {ex.Message}");
                        return new List<FileInfoItem>();
                    }
                });

                // UI 업데이트
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Files.Clear();
                    foreach (var file in fileInfos)
                    {
                        Files.Add(file);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"File_input : {ex.Message}");
            }
        }
        public async void File_input_Event()
        {
            await File_input();
        }
        public bool ReadMode()
        {
            Random random = new Random();
            string exePath = AppDomain.CurrentDomain.BaseDirectory;

            string sourceDbFile = Path.Combine(exePath, $"{projectname}.db");
            string sourceLogFile = Path.Combine(exePath, $"{projectname}-log.db");

            for (int attempt = 0; attempt < 100; attempt++)
            {
                try
                {
                    // 기존 DB 닫기
                    DatabaseProvider.Dispose();
                    Thread.Sleep(50); // OS 잠금 해제 대기

                    // 기존 복사본 삭제
                    if (!string.IsNullOrEmpty(user_file)) DeleteFileSafely(user_file);
                    if (!string.IsNullOrEmpty(user_log)) DeleteFileSafely(user_log);

                    int rnd = random.Next(1000);
                    string targetFileName = $"{projectname}_{rnd}";
                    string targetDbFile = Path.Combine(exePath, $"{targetFileName}.db");
                    string targetLogFile = Path.Combine(exePath, $"{targetFileName}-log.db");

                    // 안전하게 DB 파일 복사
                    const int retries = 5;
                    bool copySuccess = false;
                    for (int i = 0; i < retries; i++)
                    {
                        try
                        {
                            using (var src = new FileStream(sourceDbFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var dest = new FileStream(targetDbFile, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                src.CopyTo(dest);
                            }

                            using (var srcLog = new FileStream(sourceLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var destLog = new FileStream(targetLogFile, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                srcLog.CopyTo(destLog);
                            }

                            copySuccess = true;
                            break;
                        }
                        catch (IOException)
                        {
                            Thread.Sleep(50); // 잠금 대기 후 재시도
                        }
                    }

                    if (!copySuccess)
                    {
                        Debug.WriteLine("[READ MODE COPY ERROR] Failed after retries.");
                        continue;
                    }

                    // DB 초기화
                    DatabaseProvider.Initialize(false, targetFileName);
                    user_file = targetDbFile;
                    user_log = targetLogFile;
                    Thread.Sleep(100); // DB 초기화 안정화

                    // 안전 검사
                    try
                    {
                        var test = DatabaseProvider.Child_Node.FindOne(Query.All());
                    }
                    catch (LiteDB.LiteException ex)
                    {
                        Debug.WriteLine($"[READ MODE SAFETY CHECK FAILED] {ex.Message}");
                        continue;
                    }

                    return true; // 성공
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[READ MODE ERROR] {ex.Message}");
                }
            }

            return false; // 모든 시도 실패
        }
        private void DeleteFileSafely(string path)
        {
            if (!File.Exists(path))
                return;

            const int maxRetries = 10;
            const int delayMs = 200;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.Delete(path);
                    if (!File.Exists(path))
                        break;
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMs);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(delayMs);
                }
            }
        }
        public Child_File Extrude_Indiviaul(Child_File source_node, string source_file)
        {
            bool check = Read_Respone(source_file, "Extrude_Indiviaul");
            if (!check) { return null; }
            else
            {
                (List<string>, List<string>) autocad_text = cad.WorkFlow_Zwcad(source_file);

                source_node.Feature = autocad_text.Item1;
                source_node.list = autocad_text.Item2;

                return source_node;
            }
        }
        private void Extrude(DateTime time)
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_nodes = child_db.FindAll().Select(x => x.File_FullName);

                if (child_nodes == null) { return; }
                foreach (string fullName in child_nodes)
                {
                    var child_node = child_db.FindOne(x => x.File_FullName == fullName);
                    if (!File.Exists(child_node.File_FullName)) { continue; }

                    string sourcefile = child_node.File_FullName;
                    string targetfile = child_node.File_FullName;

                    byte[] has = file_check.Hash_Allocated_Unique(fullName);

                    var source_node = new Child_File();
                    source_node = watcher.SettingSourceNode(fullName, child_node.Key, has, child_node);
                    string Event = "First Created";

                    source_node.Event.Add(new EventEntry()
                    {
                        Key = source_node.Key,
                        Time = time,
                        Type = Event,
                    });

                    _db.Child_File_Table(source_node, null, DbAction.Update);
                    watcher.File_Copy(child_node.File_FullName, time, child_node.Key, Event);
                    File_input_Event();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Extrude : {ex.Message}");
            }
        }
        public async Task Extrude_btn(DateTime time)
        {
            await Task.Run(() => Extrude(time));
        }
        public void File_Description()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_node = child_db.FindOne(x => x.File_FullName == selectedItem);
                if (child_node == null) { return; }
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Tag.Clear();
                    Ref.Clear();

                    if (child_node.Feature != null)
                    {
                        foreach (var feature in child_node.Feature)
                        {
                            Tag.Add(feature);
                        }
                    }
                    if (child_node.list != null)
                    {
                        foreach (var list in child_node.list)
                        {
                            Ref.Add(list);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"File_Description : {ex.Message}");
            }
        }
        public Pdf_ium_Viewer PdfBit = new Pdf_ium_Viewer();
        public Child_File Pdf_Bitmap_Indiviaul(DateTime time, Child_File source_node, string source_file)
        {
            bool check = Read_Respone(source_file, "Pdf_Bitmap_Indiviaul");
            if (!check) { return null; }
            else
            {
                if (System.IO.Path.GetExtension(source_file).ToUpper() == ".DWG" || System.IO.Path.GetExtension(source_file).ToUpper() == ".DXF")
                {
                    string pathpdf = System.IO.Path.ChangeExtension(source_file, ".pdf");
                    byte[] data = PdfBit.RenderPdfPage(pathpdf);

                    source_node.Image.Add(new ImageEntry
                    {
                        Key = source_node.Key,
                        Data = data,
                        Time = time
                    });

                    if (source_node.Image.Count() == 0)
                        Debug.WriteLine("1");
                    File.Delete(pathpdf);
                    return source_node;
                }
                else { return null; }
            }
        }
        private void Pdf_Bitmap(DateTime time)
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_nodes = child_db.FindAll().Select(x => x.File_FullName);
                if (child_nodes == null) { return; }
                foreach (string path in child_nodes)
                {
                    var node = child_db.FindOne(x => x.File_FullName == path);
                    if (!File.Exists(path)) { continue; }
                    var file = new FileInfo(path);
                    var source_node = Pdf_Bitmap_Indiviaul(time, node, node.File_FullName);

                    _db.Child_File_Table(source_node, null, DbAction.Upsert);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pdf_Bitmap : {ex.Message}");
            }
        }
        public async Task Pdf_Bitmap_btn(DateTime time)
        {
            await Task.Run(() => Pdf_Bitmap(time));
        }
        private void LoadPdf()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_node = child_db.FindOne(x => x.File_FullName == selectedItem);
                if (child_node == null) { return; }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (child_node.Image.Count == 0)
                    {
                        pdfpage.ResetHost();
                    }
                    else
                    {
                        host = PdfBit.Pdf_Created(child_node.Image[child_node.Image.Count - 1].Data);
                        Main_Image = child_node.Image[child_node.Image.Count - 1].Data;
                    }
                    pdfpage.ResetHost();
                    pdfpage2.ResetHost();
                    pdfpage.SetHost(host);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPdf : {ex.Message}");
            }
        }
        public async Task Pdf_Load_btn()
        {
            await Task.Run(() => LoadPdf());
        }
        private void LoadPdf2(EventEntry selected)
        {
            try
            {
                var _db = DatabaseProvider.Child_Node;

                var parentNode = _db.FindAll()
                  .FirstOrDefault(x => x.Key == selected.Key && x.Image.Any(ev => ev.Time == selected.Time));

                if (parentNode == null || selected.Type == "Deleted")
                {
                    pdfpage2.ResetHost();
                    return;
                }
                if (parentNode != null && selected.Type != "Deleted")
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        int eventIndex = parentNode.Image.FindIndex(ev => ev.Time == selected.Time);

                        if (eventIndex >= 0 && eventIndex < parentNode.Image.Count)
                        {
                            var imageData = parentNode.Image[eventIndex].Data;
                            Compare_Image = imageData;
                            pdfpage2.ResetHost();
                            host2 = PdfBit.Pdf_Created(imageData);
                            pdfpage2.SetHost(host2);
                        }
                        else
                        {
                            pdfpage2.ResetHost();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPdf2 : {ex.Message}");
            }
        }
        public void Pdf_Load_btn2(EventEntry selected)
        {
            LoadPdf2(selected);
        }
        public void OpenPdfInNewWindow()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_node = child_db.FindOne(x => x.File_FullName == selectedItem);
                if (child_node == null) { return; }
                var window = new System.Windows.Window
                {
                    Title = "PDF Viewer",
                    Width = 800,
                    Height = 600
                };

                if (child_node.Image.Count == 0)
                {
                    return;
                }
                new_host = PdfBit.Pdf_Created(child_node.Image[child_node.Image.Count - 1].Data);

                var grid = new Grid();
                grid.Children.Add(new_host);
                window.Content = grid;
                window.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenPdfInNewWindow : {ex.Message}");
            }
        }
        public async Task File_Nmae_Enter_SearchAsync(string text)
        {
            try
            {
                string searchText = string.IsNullOrWhiteSpace(text) ? "" : text.Replace(" ", "").ToUpper();

                var filesWithKey = await Task.Run(() =>
                {
                    var child_db = DatabaseProvider.Child_Node;
                    var child_nodes = child_db.FindAll();

                    if (!string.IsNullOrEmpty(searchText))
                    {
                        return child_nodes
                            .Where(node => node.File_FullName.Replace(" ", "").ToUpper().Contains(searchText))
                            .Select(node => new FileInfoItem
                            {
                                Key = node.Key,
                                FilePath = node.File_FullName
                            })
                            .ToList();
                    }
                    else
                    {
                        return child_nodes
                            .Select(node => new FileInfoItem
                            {
                                Key = node.Key,
                                FilePath = node.File_FullName
                            })
                            .ToList();
                    }
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Files.Clear();
                    foreach (var file in filesWithKey)
                    {
                        Files.Add(file);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"File_Name_Enter_SearchAsync : {ex.Message}");
            }
        }
        public async Task Tag_Enter_SearchAsync(string text)
        {
            try
            {
                var searchText = string.IsNullOrWhiteSpace(text) ? "" : text.Replace(" ", "");

                var filesWithKey = await Task.Run(() =>
                {
                    var child_db = DatabaseProvider.Child_Node;
                    var child_nodes = child_db.FindAll();

                    if (!string.IsNullOrWhiteSpace(searchText))
                    {
                        return child_nodes
                            .Where(node => node.Feature != null &&
                                   node.Feature.Any(f => f != null && f.Replace(" ", "")
                                        .IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0))
                            .Select(node => new FileInfoItem
                            {
                                Key = node.Key,
                                FilePath = node.File_FullName
                            })
                            .ToList();
                    }
                    else
                    {
                        return child_nodes
                            .Select(node => new FileInfoItem
                            {
                                Key = node.Key,
                                FilePath = node.File_FullName
                            })
                            .ToList();
                    }
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Files.Clear();
                    foreach (var file in filesWithKey)
                    {
                        Files.Add(file);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tag_Enter_SearchAsync : {ex.Message}");
            }
        }
        public void Reset()
        {
            _db.Child_File_Table(null, null, DbAction.DeleteAll);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string folder = System.IO.Path.Combine(baseDir, $"repository\\{Bridge.projectname}");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                host = new WindowsFormsHost();
                host2 = new WindowsFormsHost();
                pdfpage.ResetHost();
                pdfpage2.ResetHost();
            });
        }
        public void Open_File()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var item = child_db.FindOne(x => x.File_FullName == selectedItem);

                if (item == null)
                {
                    Debug.WriteLine($"파일 '{selectedItem}'을(를) 찾을 수 없습니다.");
                    return;
                }

                string path = item.File_FullName;

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"파일 열기 실패: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"파일 경로가 유효하지 않음: {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Dubliclick_Open : {ex.Message}");
            }
        }
        public void Open_Folder()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var item = child_db.FindOne(x => x.File_FullName == selectedItem);

                if (item == null)
                {
                    Debug.WriteLine($"파일 '{selectedItem}'을(를) 찾을 수 없습니다.");
                    return;
                }

                string path = item.File_FullName;
                string folderPath = System.IO.Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = folderPath,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"폴더 열기 실패: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"폴더 경로가 유효하지 않음: {folderPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Open_Folder : {ex.Message}");
            }
        }
        public void Data_View()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var file_nodes = child_db.FindOne(x => x.File_FullName == selectedItem);
                if (file_nodes == null) { return; }
                if (file_nodes != null)
                {
                    File_History.Clear();

                    if (file_nodes.Event != null)
                    {
                        foreach (var list in file_nodes.Event)
                        {
                            File_History.Add(list);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Data_View : {ex.Message}");
            }
        }
        public void Pdf_Compare()
        {
            try
            {
                var differences = PdfBit.GetDifferences(Compare_Image, Main_Image);

                using (var bmp = PdfiumViewer.PdfDocument.Load(new MemoryStream(Compare_Image))
                                            .Render(0, 2000, 2000, true))
                {
                    var annotatedPdfBytes = PdfBit.AnnotatePdf(Compare_Image, differences, bmp.Width, bmp.Height);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Pdf_Compare_Reuslt(annotatedPdfBytes);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pdf_Compare : {ex.Message}");
            }
        }
        public void Pdf_Compare_Reuslt(byte[] annotatedPdfBytes)
        {
            try
            {
                var window = new System.Windows.Window
                {
                    Title = "PDF Viewer",
                    Width = 800,
                    Height = 600
                };
                new_host2 = PdfBit.Pdf_Created(annotatedPdfBytes);

                var grid = new Grid();
                grid.Children.Add(new_host2);
                window.Content = grid;
                window.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pdf_Compare_Reuslt : {ex.Message}");
            }
        }
        public async Task Pdf_Compare_btn()
        {
            await Task.Run(() => Pdf_Compare());
        }
        public bool Read_Respone(string path, string point, string eventname = null)
        {
            int retry = 100;
            while (retry-- > 0)
            {
                if (eventname == "Deleted")
                {
                    return false;
                }

                bool check_ext = FilterExt(path);
                if (!check_ext) return false;

                if (File.Exists(path))
                {
                    try
                    {
                        using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            return true;
                        }
                    }
                    catch (IOException) { }
                }

                Thread.Sleep(100);
                Debug.WriteLine($"[{point}] 파일 접근 실패, 남은 시도: {retry}");
            }

            Debug.WriteLine($"[{point}] 최종 실패: 파일 접근 불가");
            return false;
        }
        public bool FilterExt(string path)
        {
            if (path.Contains(".log"))
                return false;

            if (path.Contains(".pdf"))
                return false;

            if (System.IO.Path.GetExtension(path) == "")
                return false;

            if (System.IO.Path.GetExtension(path).ToUpper() != ".DWG" && System.IO.Path.GetExtension(path).ToUpper() != ".DWF")
                return false;

            return true;
        }
        public void FolderWatcher()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
            _watcher = new FileSystemWatcher(folderpath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.CreationTime
                             | NotifyFilters.FileName
                             | NotifyFilters.LastWrite
            };
            _watcher.InternalBufferSize = 64 * 1024;
            watcher.SetupWatcher(_watcher);
        }
        public void FolderWatcher_repository()
        {
            if (_watcher2 != null)
            {
                _watcher2.EnableRaisingEvents = false;
                _watcher2.Dispose();
                _watcher2 = null;
            }
            _watcher2 = new FileSystemWatcher(repository_path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.CreationTime
                             | NotifyFilters.FileName
                             | NotifyFilters.LastWrite
            };
            _watcher2.InternalBufferSize = 64 * 1024;
            watcher.SetupWatcher_repository(_watcher2);
        }
        public void Event_History_Add(string evt)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Event_History.Add(evt);
            });
        }
    }
}
