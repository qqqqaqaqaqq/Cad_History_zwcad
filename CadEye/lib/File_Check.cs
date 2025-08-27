using CadEye.ViewCS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CadEye.Lib
{
    public class File_Check
    {

        /// <summary>
        /// 폴더 내부 파일을 병렬로 찾은 후
        /// 데이터 베이스에 넣음
        /// 데이터 베이스 이벤트는 Lib.Data_base에 조건 함수 정리 해둠
        /// item_insert Data base에서 node 형식 가져왔으므로 주의.
        /// </summary>

        private Data_base _db = new Data_base();
        public Bridge vm
        {
            get { return Bridge.Instance; }
        }
        public void Setting()
        {
            _db.Child_File_Table(null, null, DbAction.DeleteAll);
        }
        public ConcurrentBag<Child_File> AllocateData(string path)
        {
            if (path == null) { return null; }
            try
            {
                var childdb = DatabaseProvider.Child_Node;
                var dirInfo = new DirectoryInfo(path);
                var fileSystemEntries = dirInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
                    .Where(f => (f.Attributes & FileAttributes.ReparsePoint) == 0)
                    .ToList();
                var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

                var item_insert = new ConcurrentBag<Child_File>();

                long accessNumber = 0;
                var keys = childdb.FindAll().Select(x => (long?)x.Key);
                long maxKey = keys.Any() ? keys.Max().Value : 0;
                accessNumber = maxKey;

                Parallel.ForEach(fileSystemEntries, options, file =>
                {
                    if (file.Extension.ToUpper() == ".DWG" || file.Extension.ToUpper() == ".DXF")
                    {
                        if ((file.Attributes & FileAttributes.Directory) == 0)
                        {
                            string[] file_name_parts = file.FullName.Split('\\');
                            string relativePath = file_name_parts[file_name_parts.Length - 2] + "\\" + file_name_parts[file_name_parts.Length - 1];
                            byte[] hash = Hash_Allocated_Unique(file.FullName);

                            bool check = childdb.FindAll().Any(x => x.File_Path == file.FullName && x.HashToken.SequenceEqual(hash)); // Data_base에서 가져옴.
                            if (check) { return; }
                            accessNumber = System.Threading.Interlocked.Increment(ref accessNumber);

                            var node = new Child_File();

                            node.Key = accessNumber;
                            node.File_Path = file.FullName;
                            node.File_Name = relativePath;
                            node.HashToken = hash;
                            node.Event = new List<EventEntry>();
                            node.Image = new List<ImageEntry>();

                            item_insert.Add(node);
                        }
                    }
                });

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                return item_insert;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }
        public byte[] Hash_Allocated_Unique(string fullName)
        {
            bool check = vm.Read_Respone(fullName, "Hash_Allocated_Unique");
            if (!check) { return null; }
            {
                var fileName = Path.GetFileName(fullName);

                if (fileName.StartsWith("~$"))
                    return Array.Empty<byte>();

                byte[] contentHash = null;
                int retries = 3;

                while (retries-- > 0)
                {
                    try
                    {
                        using (var stream = new FileStream(fullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var sha = SHA256.Create())
                        {
                            contentHash = sha.ComputeHash(stream);
                        }
                        break;
                    }
                    catch (IOException)
                    {
                        if (retries == 0)
                        {
                            Debug.WriteLine($"파일 잠김: {fullName}");
                            return Array.Empty<byte>();
                        }
                        Task.Delay(100).Wait();
                    }
                }

                string combined = fileName + Convert.ToBase64String(contentHash);
                using (var sha = SHA256.Create())
                {
                    return sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
                }
            }
        }
    }
}
