using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Scanner.Services
{
    public static class ChecklistDataCache
    {
        private static IReadOnlyList<InboundChecklistRecord> _records = new ReadOnlyCollection<InboundChecklistRecord>(new List<InboundChecklistRecord>());
        private static IReadOnlyList<string> _serialNumbers = new ReadOnlyCollection<string>(new List<string>());

        public static IReadOnlyList<InboundChecklistRecord> Records { get { return _records; } }
        public static IReadOnlyList<string> SerialNumbers { get { return _serialNumbers; } }
        public static string BaseDataFile { get; private set; }
        public static string SnFile { get; private set; }
        public static DateTime? BaseDataImportedAt { get; private set; }
        public static DateTime? SnImportedAt { get; private set; }

        public static void ReplaceRecords(IList<InboundChecklistRecord> records, string sourceFile)
        {
            _records = new ReadOnlyCollection<InboundChecklistRecord>(new List<InboundChecklistRecord>(records));
            BaseDataFile = sourceFile;
            BaseDataImportedAt = DateTime.Now;
        }

        public static void ReplaceSerialNumbers(IList<string> serialNumbers, string sourceFile)
        {
            _serialNumbers = new ReadOnlyCollection<string>(new List<string>(serialNumbers));
            SnFile = sourceFile;
            SnImportedAt = DateTime.Now;
        }
    }
}
