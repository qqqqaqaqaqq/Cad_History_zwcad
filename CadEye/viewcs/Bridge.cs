using CadEye.Lib;
using CadEye.View;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;

namespace CadEye.ViewCS
{
    public class FileInfoItem
    {
        public long Key { get; set; }
        public string FileName { get; set; }
    }

    public class Bridge
    {
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


        public Data_base _db = new Data_base();
        public string selectedItem { get; set; }

        /// <summary>
        /// 초기 로드 이벤트
        /// 파일 불러와서 db 셋팅하기
        /// 백그라운드 실행
        /// </summary>
        private string folderpath { get; set; }
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



        /// <summary>
        /// List Box에 파일 데이터 담아주는 역할
        /// UI 실행
        /// </summary>
        public ObservableCollection<FileInfoItem> Files { get; set; } = new ObservableCollection<FileInfoItem>();
        private async Task File_input()
        {
            try
            {
                if (!MainWindow.isManger)
                {
                    bool check = ReadMode();
                    if (!check) { return; }
                }
                var fileInfos = await Task.Run(() =>
                 {
                     var child_db = DatabaseProvider.Child_Node;
                     var child_nodes = child_db.FindAll();
                     var files = child_nodes
                         .Select(node => new FileInfoItem
                         {
                             Key = node.Key,
                             FileName = node.File_Name
                         })
                         .ToList();
                     return files;
                 });

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



        /// <summary>
        /// 읽기 모드
        /// </summary>
        public string user_db;
        public string user_file;
        public string user_log;
        public bool ReadMode()
        {
            Random random = new Random();
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            string sourceFile = System.IO.Path.Combine(exePath, $"{projectname}-log.db");

            for (int num = 0; num < 100; num++)
            {
                try
                {

                    DatabaseProvider.Dispose();
                    if (user_file != null)
                    {
                        DeleteFileSafely(user_file);
                        DeleteFileSafely(user_log);
                    }
                    string targetFile = "";
                    string targetFileName = "";
                    string targetlogFile = "";

                    int rnd = random.Next(1000);
                    targetFileName = $"{projectname}_{rnd}";
                    targetFile = System.IO.Path.Combine(exePath, $"{targetFileName}.db");
                    targetlogFile = System.IO.Path.Combine(exePath, $"{targetFileName}-log.db");
                    File.Copy(sourceFile, targetlogFile, true);
                    DatabaseProvider.Initialize(false, targetFileName);
                    user_file = targetFile;
                    user_log = targetlogFile;

                    return true;
                }
                catch (IOException ioEx)
                {
                    Debug.WriteLine($"[READ MODE COPY ERROR] {ioEx.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[READ MODE ERROR] {ex.Message}");
                }
            }
            return false;
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

        /// <summary>
        /// 지더블유 캐드 텍스트 추출 이벤트
        /// 백그라운드 실행
        /// </summary>
        public ZwCad_Lib cad = new ZwCad_Lib();
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
        private void Extrude()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_nodes = child_db.FindAll().Select(x => x.File_Path);
                if (child_nodes == null) { return; }
                foreach (string path in child_nodes)
                {
                    var node = child_db.FindOne(x => x.File_Path == path);
                    if (!File.Exists(node.File_Path)) { continue; }
                    var file = new FileInfo(node.File_Path);
                    DateTime lastWriteTime = file.LastWriteTime;
                    lastWriteTime = lastWriteTime.AddTicks(-(lastWriteTime.Ticks % TimeSpan.TicksPerSecond));
                    if (node.Image.Count != 0)
                    { if (node.Image[node.Image.Count - 1].Time == lastWriteTime) { continue; } }
                    var source_node = Extrude_Indiviaul(node, node.File_Path);
                    _db.Child_File_Table(source_node, null, DbAction.Upsert);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Extrude : {ex.Message}");
            }
        }
        public async Task Extrude_btn()
        {
            await Task.Run(() => Extrude());
        }


        /// <summary>
        /// 리스트 클릭 시 오토캐드에서 추출한 문자열 화면에 표시
        /// </summary>
        public ObservableCollection<string> Tag { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> Ref { get; set; } = new ObservableCollection<string>();
        public void File_Description()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_node = child_db.FindOne(x => x.File_Name == selectedItem);
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


        /// <summary>
        /// DB에 Pdf 이미지 삽입 벡터 형식
        /// </summary>
        public Pdf_ium_Viewer PdfBit = new Pdf_ium_Viewer();
        /// <summary>
        /// Pdf 이미지 Bitmap
        /// </summary>
        public Child_File Pdf_Bitmap_Indiviaul(DateTime time, Child_File source_node, string source_file)
        {
            bool check = Read_Respone(source_file, "Pdf_Bitmap_Indiviaul");
            if (!check) { return null; }
            else
            {
                if (System.IO.Path.GetExtension(source_node.File_Path).ToUpper() == ".DWG" || System.IO.Path.GetExtension(source_node.File_Path).ToUpper() == ".DXF")
                {
                    string pathpdf = System.IO.Path.ChangeExtension(source_file, ".pdf");
                    source_node.Image.Add(new ImageEntry()
                    {
                        Data = PdfBit.RenderPdfPage(pathpdf),
                        Time = time
                    });
                    File.Delete(pathpdf);
                    return source_node;
                }
                else { return null; }
            }
        }
        private void Pdf_Bitmap()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_nodes = child_db.FindAll().Select(x => x.File_Path);
                if (child_nodes == null) { return; }
                foreach (string path in child_nodes)
                {
                    var node = child_db.FindOne(x => x.File_Path == path);
                    if (!File.Exists(path)) { continue; }
                    var file = new FileInfo(path);
                    DateTime lastWriteTime = file.LastWriteTime;
                    lastWriteTime = lastWriteTime.AddTicks(-(lastWriteTime.Ticks % TimeSpan.TicksPerSecond));
                    if (node.Image.Count != 0)
                    {
                        if (node.Image[node.Image.Count - 1].Time == lastWriteTime) { continue; }
                    }
                    var source_node = Pdf_Bitmap_Indiviaul(lastWriteTime, node, node.File_Path);

                    _db.Child_File_Table(source_node, null, DbAction.Upsert);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pdf_Bitmap : {ex.Message}");
            }
        }
        public async Task Pdf_Bitmap_btn()
        {
            await Task.Run(() => Pdf_Bitmap());
        }


        /// <summary>
        /// Main 미리보기 이미지 삽입 LoadPdf
        /// Compare 미리보기 이미지 삽입 LoadPdf2
        /// </summary>
        public PdfContainer pdfpage;
        public PdfContainer2 pdfpage2;
        public WindowsFormsHost host = new WindowsFormsHost();
        public WindowsFormsHost host2 = new WindowsFormsHost();
        public byte[] Main_Image = new byte[] { };
        public byte[] Compare_Image = new byte[] { };
        private void LoadPdf()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_node = child_db.FindOne(x => x.File_Name == selectedItem);
                if (child_node == null) { return; }
                if (child_node.Image.Count == 0)
                {
                    pdfpage.ResetHost();
                }
                host = PdfBit.Pdf_Created(child_node.Image[child_node.Image.Count - 1].Data);
                Main_Image = child_node.Image[child_node.Image.Count - 1].Data;

                pdfpage.ResetHost();
                pdfpage2.ResetHost();
                pdfpage.SetHost(host);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPdf : {ex.Message}");
            }
        }
        private void LoadPdf2(EventEntry selected)
        {
            try
            {
                var _db = DatabaseProvider.Child_Node;
                var parentNode = _db.FindAll().FirstOrDefault(x => x.Event.Any(ev => ev.Time == selected.Time));
                if (parentNode == null) { return; }
                if (parentNode != null)
                {
                    int eventIndex = parentNode.Event.FindIndex(ev => ev.Time == selected.Time);

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
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadPdf2 : {ex.Message}");
            }
        }
        public void Pdf_Load_btn()
        {
            LoadPdf();
        }
        public void Pdf_Load_btn2(EventEntry selected)
        {
            LoadPdf2(selected);
        }



        /// <summary>
        /// 이미지 새폼에서 열기
        /// </summary>
        public WindowsFormsHost new_host = new WindowsFormsHost();
        public void OpenPdfInNewWindow()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_node = child_db.FindOne(x => x.File_Name == selectedItem);
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


        /// <summary>
        /// 파일명 태그 검색 기능
        /// </summary>
        public async Task File_Nmae_Enter_SearchAsync(string text)
        {
            try
            {
                if (text != "")
                {
                    var filesWithKey = await Task.Run(() =>
                    {
                        var child_db = DatabaseProvider.Child_Node;
                        var child_nodes = child_db.FindAll();

                        return child_nodes
                            .Where(node => node.File_Name.ToUpper().Contains(text.ToUpper()))
                            .Select(node => new FileInfoItem
                            {
                                Key = node.Key,
                                FileName = node.File_Name
                            })
                            .ToList();
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
                else
                {
                    var filesWithKey = await Task.Run(() =>
                    {
                        var child_db = DatabaseProvider.Child_Node;
                        var child_nodes = child_db.FindAll();

                        return child_nodes
                            .Select(node => new FileInfoItem
                            {
                                Key = node.Key,
                                FileName = node.File_Name
                            })
                            .ToList();
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Enter_SearchAsync : {ex.Message}");
            }
        }
        public async Task Tag_Enter_SearchAsync(string text)
        {
            try
            {
                var filesWithKey = await Task.Run(() =>
                {
                    var child_db = DatabaseProvider.Child_Node;
                    var child_nodes = child_db.FindAll();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return child_nodes
                            .Where(node => node.Feature != null &&
                                   node.Feature.Any(f => f != null && f.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0))
                            .Select(node => new FileInfoItem
                            {
                                Key = node.Key,
                                FileName = node.File_Name
                            })
                            .ToList();
                    }
                    else
                    {
                        return child_nodes
                            .Select(node => new FileInfoItem
                            {
                                Key = node.Key,
                                FileName = node.File_Name
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


        /// <summary>
        /// 데이터 리셋
        /// </summary>
        public void Reset()
        {
            _db.Child_File_Table(null, null, DbAction.DeleteAll);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string folder = System.IO.Path.Combine(baseDir, $"repository\\{Bridge.projectname}");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }



        /// <summary>
        /// 폴더, 파일 열기
        /// </summary>
        public void Open_File()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var item = child_db.FindOne(x => x.File_Name == selectedItem);

                if (item == null)
                {
                    Debug.WriteLine($"파일 '{selectedItem}'을(를) 찾을 수 없습니다.");
                    return;
                }

                string path = item.File_Path;

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
                var item = child_db.FindOne(x => x.File_Name == selectedItem);

                if (item == null)
                {
                    Debug.WriteLine($"파일 '{selectedItem}'을(를) 찾을 수 없습니다.");
                    return;
                }

                string path = item.File_Path;
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




        /// <summary>
        /// 파일 와처로 인한 히스토리 뷰
        /// </summary>
        public ObservableCollection<EventEntry> File_History { get; set; } = new ObservableCollection<EventEntry>();
        public void Data_View()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var file_nodes = child_db.FindOne(x => x.File_Name == selectedItem);
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




        /// <summary>
        /// 2개의 PDF 비교 기능
        /// </summary>
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

        public WindowsFormsHost new_host2 = new WindowsFormsHost();
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




        /// <summary>
        /// 접근 실패 재시도 함수
        /// </summary>

        public bool Read_Respone(string path, string point)
        {
            int retry = 120;
            while (retry-- > 0)
            {
                if (System.IO.Path.GetExtension(path).ToUpper() != ".DWG" && System.IO.Path.GetExtension(path).ToUpper() != ".DWF")
                {
                    return false;
                }

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

                Thread.Sleep(500);
                Debug.WriteLine($"[{point}] 파일 접근 실패, 남은 시도: {retry}");
            }

            Debug.WriteLine($"[{point}] 최종 실패: 파일 접근 불가");
            return false;
        }

        /// <summary>
        /// watcher
        /// </summary>

        File_Watcher watcher = new File_Watcher();
        public FileSystemWatcher _watcher;
        public FileSystemWatcher _watcher2;
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
    }
}
