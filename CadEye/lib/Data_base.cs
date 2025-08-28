using CadEye.ViewCS;
using LiteDB;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CadEye.Lib
{
    public static class DatabaseProvider
    {
        private static LiteDatabase _instance;
        public static bool IsManager { get; private set; }

        public static void Initialize(bool isManager, string dbFileName)
        {
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            string dbPath = Path.Combine(exePath, dbFileName);

            IsManager = isManager;

            if (isManager)
            {
                _instance = new LiteDatabase($"{dbPath}.db");
            }
            else
            {
                _instance = new LiteDatabase($"Filename={dbPath}.db; Mode=ReadOnly;");
            }
        }
        public static LiteDatabase Instance => _instance
            ?? throw new InvalidOperationException("DatabaseProvider is not initialized!");
        public static ILiteCollection<Child_File> Child_Node => Instance.GetCollection<Child_File>("Child_Node");
        public static void Dispose()
        {
            _instance?.Dispose();
            _instance = null;
        }
    }



    public class ImageEntry
    {
        public long Key { get; set; }
        public DateTime Time { get; set; }
        public byte[] Data { get; set; }
    }
    public class EventEntry
    {
        public long Key { get; set; }
        public DateTime Time { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
    }
    public class Child_File
    {
        public Child_File()
        {
            Image = new List<ImageEntry>();
            Event = new List<EventEntry>();
            Feature = new List<string>();
            list = new List<string>();
        }

        [BsonId]
        public long Key { get; set; }
        public string File_Path { get; set; } = "";
        public byte[] HashToken { get; set; }
        public string File_Name { get; set; } = "";
        public List<string> Feature { get; set; }
        public List<string> list { get; set; }
        public List<EventEntry> Event { get; set; }
        public List<ImageEntry> Image { get; set; }
    }
    public enum DbAction
    {
        Upsert,
        Delete,
        DeleteAll,
        AllUpsert,
        Update,
        Insert,  
    }

    public class Data_base
    {
        public readonly object _lock = new object();
        public bool Child_File_Table(Child_File node, ConcurrentBag<Child_File> list, DbAction action)
        {
            var child_db = DatabaseProvider.Child_Node;
            try
            {
                lock (_lock)
                {
                    switch (action)
                    {
                        case DbAction.Upsert:
                            child_db.Upsert(node);
                            break;

                        case DbAction.Delete:
                            child_db.Delete(node.Key);
                            break;

                        case DbAction.DeleteAll:
                            DatabaseProvider.Instance.DropCollection("Child_Node");
                            break;

                        case DbAction.AllUpsert:
                            child_db.Upsert(list);
                            break;

                        case DbAction.Update:
                            child_db.Update(node);
                            break;

                        case DbAction.Insert:
                            child_db.Insert(node);
                            break;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "알림");
                return false;
            }
        }
    }
}
