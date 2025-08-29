using CadEye.ViewCS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CadEye.Lib
{
    public class File_Watcher
    {
        public Data_base _db = new Data_base();
        public File_Check file_check = new File_Check();
        public ZwCad_Lib cad = new ZwCad_Lib();
        private ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private System.Timers.Timer _timer;

        public Bridge vm
        {
            get { return Bridge.Instance; }
        }


        // --------------------------------------------------------------------------------- 폴더에 대한 감시
        public void SetupWatcher(FileSystemWatcher _watcher)
        {
            Task.Run(Brdige_Queue);
            _watcher.Changed += (s, e) => Bridge_Event(s, e);
            _watcher.Created += (s, e) => Bridge_Event(s, e);
            _watcher.Deleted += (s, e) => Bridge_Event(s, e);
            _watcher.Renamed += (s, e) => Bridge_Event(s, e);
            _watcher.EnableRaisingEvents = true;
        }

        private List<(FileSystemEventArgs, string, DateTime)> source_list = new List<(FileSystemEventArgs, string, DateTime)>();
        private ConcurrentQueue<FileSystemEventArgs> eventQueue = new ConcurrentQueue<FileSystemEventArgs>();
        private bool isCollecting = false;

        public async void Bridge_Event(object sender, FileSystemEventArgs e)
        {
            bool check_ext = vm.FilterExt(e.FullPath);
            if (!check_ext) return;
            DateTime current_time = DateTime.Now;
            source_list.Add((e, e.FullPath, current_time));

            if (!isCollecting)
            {
                if (source_list.Count() > 0)
                {
                    isCollecting = true;
                    await Task.Delay(300);

                    List<(FileSystemEventArgs, string, DateTime)> target_list =
                        new List<(FileSystemEventArgs, string, DateTime)>();
                    List<FileSystemEventArgs> filter_list = new List<FileSystemEventArgs>();

                    foreach (var list in source_list)
                    {
                        target_list.Add(list);
                    }
                    filter_list = Detected(target_list);
                    foreach (var list in filter_list.ToList())
                    {
                        eventQueue.Enqueue(list);
                    }
                    isCollecting = false;
                }
            }
        }
        public List<FileSystemEventArgs> Detected(List<(FileSystemEventArgs, string, DateTime)> target_list)
        {
            Dictionary<string, FileSystemEventArgs> uniqueEvents = new Dictionary<string, FileSystemEventArgs>();

            foreach (var item in target_list)
            {
                string path = item.Item2;
                FileSystemEventArgs args = item.Item1;
                uniqueEvents[path] = args;
            }

            foreach (var path in uniqueEvents.Keys)
            {
                source_list.RemoveAll(x => x.Item2 == path);
            }

            return uniqueEvents.Values.ToList();
        }
        public async Task Brdige_Queue()
        {
            while (true)
            {
                if (eventQueue.TryDequeue(out var e))
                {
                    DateTime time = new DateTime(
                        DateTime.Now.Year,
                        DateTime.Now.Month,
                        DateTime.Now.Day,
                        DateTime.Now.Hour,
                        DateTime.Now.Minute,
                        DateTime.Now.Second
                    );
                    switch (e.ChangeType)
                    {
                        case WatcherChangeTypes.Created:
                            File_A(e, time);
                            break;
                        case WatcherChangeTypes.Changed:
                            File_A(e, time);
                            break;
                        case WatcherChangeTypes.Deleted:
                            File_A(e, time);
                            break;
                        case WatcherChangeTypes.Renamed:
                            File_B(e, time);
                            break;
                    }
                }
                else
                {
                    await Task.Delay(50);
                }
            }
        }
        private void File_A(FileSystemEventArgs e, DateTime time)
        {
            try
            {
                bool read_chk = vm.Read_Respone(e.FullPath, "File_A", e.ChangeType.ToString());
                if (!read_chk)
                {
                    // Deleted
                    var child_node = DatabaseProvider.Child_Node;
                    var target_node = child_node.FindOne(x => x.File_Path == e.FullPath);

                    var foldername = Path.GetDirectoryName(target_node.File_Path);
                    var relative = Path.GetFileName(foldername);
                    var relativefilename = Path.Combine(relative, Path.GetFileName(target_node.File_Path));
                    var source_node = new Child_File();

                    source_node.Key = target_node.Key;
                    source_node.File_Path = e.FullPath;
                    source_node.HashToken = target_node.HashToken;
                    source_node.File_Name = target_node.File_Name;
                    source_node.list = target_node.list;
                    source_node.Feature = target_node.Feature;
                    source_node.Event = target_node.Event;
                    source_node.Image = target_node.Image;
                    source_node.Detele_Check = 1;
                    string Event = "Deleted";

                    source_node.Event.Add(new EventEntry()
                    {
                        Key = source_node.Key,
                        Time = time,
                        Type = Event,
                        Description = $"삭제 : {relativefilename}"
                    });


                    if (source_node.Image.Count() > 0)
                    {
                        source_node.Image.Add(new ImageEntry()
                        {
                            Key = source_node.Key,
                            Time = time,
                            Data = target_node.Image[target_node.Image.Count() - 1].Data,
                        });
                    }

                    _db.Child_File_Table(source_node, null, DbAction.Update);
                    vm.File_input_Event();
                    return;
                }
                else
                {
                    var child_node = DatabaseProvider.Child_Node;
                    var target_node = child_node.FindOne(x => x.File_Path == e.FullPath);

                    var source_node = new Child_File();
                    long key = 0;
                    byte[] has;

                    has = file_check.Hash_Allocated_Unique(e.FullPath);
                    string fileName = Path.GetFileName(e.FullPath);
                    string Event = "";

                    if (target_node == null)
                    {
                        var all_nodes = child_node.FindAll();
                        var target_node_hash = all_nodes.FirstOrDefault(x => x.HashToken.SequenceEqual(has));
                        if (target_node_hash == null)
                        {
                            // Created
                            var allkeys = child_node.FindAll().Select(x => x.Key);
                            key = allkeys.Any() ? allkeys.Max() + 1 : 1;

                            source_node.Key = key;
                            source_node.File_Path = e.FullPath;
                            source_node.File_Name = fileName;
                            source_node.HashToken = has;
                            source_node.list = new List<string>();
                            source_node.Feature = new List<string>();
                            source_node.Event = new List<EventEntry>();
                            source_node.Image = new List<ImageEntry>();
                            source_node.Detele_Check = 0;

                            Event = "Created";

                            _db.Child_File_Table(source_node, null, DbAction.Insert);
                            File_Copy(e, time, key, Event);
                            vm.File_input_Event();
                        }
                        else
                        {
                            if (!File.Exists(target_node_hash.File_Path))
                            {
                                // Moved
                                key = target_node_hash.Key;
                                var foldername = Path.GetDirectoryName(e.FullPath);
                                var relative = Path.GetFileName(foldername);
                                var relativefilename = Path.Combine(relative, Path.GetFileName(e.FullPath));

                                source_node.Key = key;
                                source_node.File_Path = e.FullPath;
                                source_node.File_Name = fileName;
                                source_node.HashToken = has;
                                source_node.list = target_node_hash.list;
                                source_node.Feature = target_node_hash.Feature;
                                source_node.Event = target_node_hash.Event;
                                source_node.Image = target_node_hash.Image;
                                source_node.Detele_Check = 0;
                                Event = "Moved";

                                source_node.Event.Add(new EventEntry()
                                {
                                    Key = source_node.Key,
                                    Time = time,
                                    Type = Event,
                                    Description = $"위치 : {relativefilename}",
                                });


                                if (source_node.Image.Count() > 0)
                                {
                                    source_node.Image.Add(new ImageEntry()
                                    {
                                        Key = source_node.Key,
                                        Time = time,
                                        Data = target_node_hash.Image[target_node_hash.Image.Count() - 1].Data,
                                    });
                                }

                                _db.Child_File_Table(source_node, null, DbAction.Upsert);
                                vm.File_input_Event();
                                return;
                            }
                            else
                            {
                                // Copyed
                                var foldername = Path.GetDirectoryName(target_node_hash.File_Path);
                                var relative = Path.GetFileName(foldername);
                                var relativefilename = Path.Combine(relative, Path.GetFileName(target_node_hash.File_Path));

                                // Created
                                var allkeys = child_node.FindAll().Select(x => x.Key);
                                key = allkeys.Any() ? allkeys.Max() + 1 : 1;

                                source_node.Key = key;
                                source_node.File_Path = e.FullPath;
                                source_node.File_Name = fileName;
                                source_node.HashToken = has;
                                source_node.list = target_node_hash.list;
                                source_node.Feature = target_node_hash.Feature;
                                source_node.Event = target_node_hash.Event;
                                source_node.Image = target_node_hash.Image;
                                source_node.Detele_Check = 0;
                                Event = "Copyed";

                                source_node.Event.Add(new EventEntry()
                                {
                                    Key = source_node.Key,
                                    Time = time,
                                    Type = Event,
                                    Description = $"원본 : {relativefilename}"
                                });


                                if (source_node.Image.Count() > 0)
                                {
                                    source_node.Image.Add(new ImageEntry()
                                    {
                                        Key = source_node.Key,
                                        Time = time,
                                        Data = target_node_hash.Image[target_node_hash.Image.Count() - 1].Data,
                                    });
                                }

                                _db.Child_File_Table(source_node, null, DbAction.Upsert);
                                vm.File_input_Event();
                                return;
                            }
                        }
                    }
                    else
                    {
                        // Restore
                        if (target_node.Detele_Check == 1)
                        {
                            key = target_node.Key;
                            source_node.Key = key;
                            source_node.File_Path = e.FullPath;
                            source_node.File_Name = fileName;
                            source_node.HashToken = has;
                            source_node.list = target_node.list;
                            source_node.Feature = target_node.Feature;
                            source_node.Event = target_node.Event;
                            source_node.Image = target_node.Image;
                            source_node.Detele_Check = 0;
                            Event = "Restore";

                            source_node.Event.Add(new EventEntry()
                            {
                                Key = source_node.Key,
                                Time = time,
                                Type = Event,
                                Description = "복원"
                            });


                            if (source_node.Image.Count() > 0)
                            {
                                source_node.Image.Add(new ImageEntry()
                                {
                                    Key = source_node.Key,
                                    Time = time,
                                    Data = target_node.Image[target_node.Image.Count() - 1].Data,
                                });
                            }

                            _db.Child_File_Table(source_node, null, DbAction.Update);
                            vm.File_input_Event();
                            return;
                        }
                        else
                        {
                            if (target_node.HashToken.SequenceEqual(has))
                            {
                                key = target_node.Key;
                                source_node.Key = key;
                                source_node.File_Path = e.FullPath;
                                source_node.File_Name = fileName;
                                source_node.HashToken = has;
                                source_node.list = target_node.list;
                                source_node.Feature = target_node.Feature;
                                source_node.Event = target_node.Event;
                                source_node.Image = target_node.Image;
                                source_node.Detele_Check = 0;
                                Event = "No-Changed";

                                source_node.Event.Add(new EventEntry()
                                {
                                    Key = source_node.Key,
                                    Time = time,
                                    Type = Event,
                                    Description = "내용이 안변했습니다"
                                });


                                if (source_node.Image.Count() > 0)
                                {
                                    source_node.Image.Add(new ImageEntry()
                                    {
                                        Key = source_node.Key,
                                        Time = time,
                                        Data = target_node.Image[target_node.Image.Count() - 1].Data,
                                    });
                                }

                                _db.Child_File_Table(source_node, null, DbAction.Update);
                                vm.File_input_Event();
                                return;
                            }
                            else
                            {
                                // Changed
                                key = target_node.Key;
                                source_node.Key = key;
                                source_node.File_Path = e.FullPath;
                                source_node.File_Name = fileName;
                                source_node.HashToken = has;
                                source_node.list = target_node.list;
                                source_node.Feature = target_node.Feature;
                                source_node.Event = target_node.Event;
                                source_node.Image = target_node.Image;
                                source_node.Detele_Check = 0;
                                Event = "Changed";
                            }
                        }
                        _db.Child_File_Table(source_node, null, DbAction.Upsert);
                        File_Copy(e, time, key, Event);
                        vm.File_input_Event();
                    }
                }

                Debug.WriteLine($"File_A Result = true");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"File_A Result = false, {ex.Message}");
            }
        }
        private void File_B(FileSystemEventArgs e, DateTime time)
        {
            try
            {
                bool read_chk = vm.Read_Respone(e.FullPath, "File_B");
                if (!read_chk) { return; }
                else
                {
                    var re = e as RenamedEventArgs;
                    var file = new FileInfo(e.FullPath);
                    var target_node = DatabaseProvider.Child_Node.FindOne(x => x.File_Path == re.OldFullPath);
                    if (target_node == null) return;

                    var source_node = new Child_File();
                    var filename = Path.GetFileName(e.FullPath);
                    source_node.Key = target_node.Key;
                    source_node.File_Path = e.FullPath;
                    source_node.File_Name = filename;
                    source_node.HashToken = file_check.Hash_Allocated_Unique(e.FullPath);
                    source_node.list = target_node.list;
                    source_node.Feature = target_node.Feature;
                    source_node.Event = target_node.Event;
                    source_node.Image = target_node.Image;

                    source_node.Event.Add(new EventEntry
                    {
                        Key = source_node.Key,
                        Time = time,
                        Type = "Renamed",
                        Description = $"Pre Name : {re.OldFullPath}"
                    });

                    if (source_node.Image.Count > 0)
                    {
                        byte[] lastData = source_node.Image[source_node.Image.Count - 1].Data;

                        source_node.Image.Add(new ImageEntry
                        {
                            Key = source_node.Key,
                            Time = time,
                            Data = (byte[])lastData.Clone()
                        });
                    }

                    bool check = _db.Child_File_Table(source_node, null, DbAction.Upsert);
                    vm.File_input_Event();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"File_B Result = false, {ex.Message}");
            }
        }
        private void SafeFileCopy(string sourcePath, string destPath, int retries = 5, int delayMs = 500)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    File.Copy(sourcePath, destPath, true);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMs);
                }
            }
            throw new IOException($"파일 복사 실패: {sourcePath}");
        }
        private void File_Copy(FileSystemEventArgs e, DateTime time, long key, string Event)
        {
            try
            {
                bool read_chk = vm.Read_Respone(e.FullPath, "File_Copy");
                if (!read_chk) { return; }
                else
                {
                    var source_file = e.FullPath;
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string target_folder = Path.Combine(baseDir, $"repository\\{Bridge.projectname}");
                    if (!Directory.Exists(target_folder))
                    {
                        Directory.CreateDirectory(target_folder);
                    }
                    string target_file = Path.Combine(target_folder, $"{time:yyyy-MM-dd-HH-mm-ss}_{Event}_{key}.dwg");

                    const int maxRetries = 5;
                    const int delayMs = 2000;

                    for (int i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            using (FileStream stream = new FileStream(source_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                SafeFileCopy(source_file, target_file);
                            }
                            return;
                        }
                        catch (IOException)
                        {
                            if (i == maxRetries - 1) throw;
                            Thread.Sleep(delayMs);
                        }
                    }
                }
                Debug.WriteLine($"복사 성공 : {e.FullPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"File_Copy : {ex.Message}");
            }
        }
        // --------------------------------------------------------------------------------- 저장소에 대한 감시


        private ConcurrentQueue<FileSystemEventArgs> eventQueue_repository = new ConcurrentQueue<FileSystemEventArgs>();

        public void SetupWatcher_repository(FileSystemWatcher _watcher)
        {
            Task.Run(Brdige_Queue_repository);
            _watcher.Created += (s, e) => Bridge_Event_repository(s, e);
            _watcher.EnableRaisingEvents = true;
        }
        public void Bridge_Event_repository(object sender, FileSystemEventArgs e)
        {
            eventQueue_repository.Enqueue(e);
        }
        public async Task Brdige_Queue_repository()
        {
            while (true)
            {
                if (eventQueue_repository.TryDequeue(out var e))
                {
                    switch (e.ChangeType)
                    {
                        case WatcherChangeTypes.Created:
                            Repository(e);
                            break;
                    }
                }
                else
                {
                    await Task.Delay(50);
                }
            }
        }
        private void Repository(FileSystemEventArgs e)
        {
            try
            {
                bool read_chk = vm.Read_Respone(e.FullPath, "Repository");
                if (!read_chk) { return; }
                {
                    (DateTime, string, long) name_parts = file_name_parsing(e);

                    var child_node = DatabaseProvider.Child_Node;
                    var target_node = child_node.FindOne(x => x.Key == name_parts.Item3);

                    if (target_node == null)
                    {
                        return;
                    }


                    var source_node = new Child_File();

                    source_node.Key = target_node.Key;
                    source_node.File_Path = target_node.File_Path;
                    source_node.File_Name = target_node.File_Name;
                    source_node.HashToken = target_node.HashToken;
                    source_node.Event = target_node.Event;
                    source_node.Image = target_node.Image;
                    source_node.Feature = target_node.Feature;
                    source_node.list = target_node.list;

                    source_node.Event.Add(new EventEntry()
                    {
                        Key = source_node.Key,
                        Time = name_parts.Item1,
                        Type = name_parts.Item2,
                    });

                    int retry = 5;
                    while (retry-- > 0)
                    {
                        source_node = Extrude_PDF(e, source_node, name_parts.Item1);
                        if (source_node == null)
                        {
                            Thread.Sleep(1000);
                        }
                        else
                        { break; }
                    }

                    if (source_node != null)
                    {
                        bool check = _db.Child_File_Table(source_node, null, DbAction.Update);
                    }
                }
                Debug.WriteLine($"Repository Result = true");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Repository Result = false, {ex.Message}");
            }
        }
        private (DateTime, string, long) file_name_parsing(FileSystemEventArgs e)
        {
            string fileName = Path.GetFileName(e.FullPath);
            string[] parts = fileName.Split('_');
            if (parts.Length < 3)
            {
                Debug.WriteLine($"Repository_ProcessQueue : 파일 이름 형식 오류 - {fileName}");
                return (DateTime.Now, "", 0);
            }
            if (!DateTime.TryParseExact(parts[0], "yyyy-MM-dd-HH-mm-ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
            {
                Debug.WriteLine($"Repository_ProcessQueue : 날짜 파싱 실패 - {parts[0]}");
                return (DateTime.Now, "", 0);
            }
            string type = parts[1];
            if (!long.TryParse(parts[2].Split('.')[0], out long Number))
            {
                Debug.WriteLine($"Repository_ProcessQueue : 번호 파싱 실패 - {parts[2]}");
                return (DateTime.Now, "", 0);
            }

            return (time, type, Number);
        }
        private Child_File Extrude_PDF(FileSystemEventArgs e, Child_File source_node, DateTime time)
        {
            bool read_chk = vm.Read_Respone(e.FullPath, "Extrude_PDF");

            try
            {
                source_node = vm.Extrude_Indiviaul(source_node, e.FullPath);
                source_node = vm.Pdf_Bitmap_Indiviaul(time, source_node, e.FullPath);
                return source_node;
            }
            catch
            {
                Thread.Sleep(1000);
                return null;
            }
        }
        // ---------------------------------------------------------------------------------
    }
}


