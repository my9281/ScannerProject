using Scanner.Models;
using System.Collections.ObjectModel;

namespace Scanner.Helpers.Services
{
    public class ChecklistDataCache
    {
        private IReadOnlyList<InboundChecklistRecord> _records = new ReadOnlyCollection<InboundChecklistRecord>(new List<InboundChecklistRecord>());
        private IReadOnlyList<string> _serialNumbers = new ReadOnlyCollection<string>(new List<string>());

        public IReadOnlyList<InboundChecklistRecord> Records { get { return _records; } }
        public IReadOnlyList<string> SerialNumbers { get { return _serialNumbers; } }
        public string BaseDataFile { get; private set; }
        public string SnFile { get; private set; }
        public DateTime? BaseDataImportedAt { get; private set; }
        public DateTime? SnImportedAt { get; private set; }
        public MonthlyReportSource MonthlySource { get; private set; }

        public void ImportBase(string path)
        {
            var source = MonthlyReportSource.Load(path);
            IList<InboundChecklistRecord> records;
            using (var stream = new System.IO.MemoryStream(source.Workbook)) records = ChecklistXlsxReader.Read(stream);
            if (source.Rows.Count == 0) throw new System.IO.InvalidDataException("基础表没有有效数据。");
            ReplaceRecords(records, path);
            MonthlySource = source;
        }

        public void ReplaceRecords(IList<InboundChecklistRecord> records, string sourceFile)
        {
            MonthlySource = null;
            _records = new ReadOnlyCollection<InboundChecklistRecord>(new List<InboundChecklistRecord>(records));
            BaseDataFile = sourceFile;
            BaseDataImportedAt = DateTime.Now;
        }

        public void ReplaceSerialNumbers(IList<string> serialNumbers, string sourceFile)
        {
            _serialNumbers = new ReadOnlyCollection<string>(new List<string>(serialNumbers));
            SnFile = sourceFile;
            SnImportedAt = DateTime.Now;
        }
    }
}
