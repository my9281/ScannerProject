using Microsoft.VisualBasic.FileIO;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Scanner.Helpers
{
    public sealed class CsvImportHelper
    {
        private const int UrgentColumn = 9;
        private const int SnColumn = 24;
        private const int TrackingColumn = 29;
        private const int RemarkColumn = 43;

        public IReadOnlyList<WorkOrderRemark> ImportUrgentWorkOrders(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("CSV 文件路径不能为空。", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("找不到 CSV 文件。", filePath);
            }

            var result = new List<WorkOrderRemark>();
            int rowNumber = 0;
            using (var parser = new TextFieldParser(filePath, Encoding.UTF8))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                parser.TrimWhiteSpace = false;

                while (!parser.EndOfData)
                {
                    rowNumber++;
                    string[] fields;
                    try
                    {
                        fields = parser.ReadFields();
                    }
                    catch (MalformedLineException ex)
                    {
                        throw new InvalidOperationException($"CSV 第 {rowNumber} 行格式错误：{ex.Message}", ex);
                    }

                    if (IsEmpty(fields) || fields.Length < 44 || rowNumber == 1 && IsHeader(fields))
                    {
                        continue;
                    }

                    string urgentSource = GetField(fields, UrgentColumn);
                    if (urgentSource.IndexOf("维修", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    string sn = GetField(fields, SnColumn);
                    string tracking = GetField(fields, TrackingColumn);
                    if (string.IsNullOrWhiteSpace(sn) && string.IsNullOrWhiteSpace(tracking))
                    {
                        throw new InvalidOperationException($"CSV 第 {rowNumber} 行的 Y 列 SN 和 AD 列运单号不能同时为空。");
                    }

                    result.Add(new WorkOrderRemark
                    {
                        Id = $"CSV-{rowNumber}-{Guid.NewGuid():N}",
                        Sn = NullIfEmpty(sn),
                        TrackingNumber = NullIfEmpty(tracking),
                        Remark = GetField(fields, RemarkColumn),
                        IsUrgent = true,
                        RemarkTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });
                }
            }

            return result;
        }

        private static bool IsEmpty(string[] fields)
        {
            if (fields == null)
            {
                return true;
            }

            foreach (string field in fields)
            {
                if (!string.IsNullOrWhiteSpace(field))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsHeader(string[] fields)
        {
            string sn = GetField(fields, SnColumn);
            string tracking = GetField(fields, TrackingColumn);
            string remark = GetField(fields, RemarkColumn);
            return sn.IndexOf("SN", StringComparison.OrdinalIgnoreCase) >= 0 || sn.Contains("序列号") || sn.Contains("编码") ||
                   tracking.Contains("运单") || tracking.IndexOf("Tracking", StringComparison.OrdinalIgnoreCase) >= 0 || remark.Contains("备注");
        }

        private static string GetField(string[] fields, int index)
        {
            if (fields == null || index < 0 || index >= fields.Length || fields[index] == null)
            {
                return string.Empty;
            }

            return fields[index].Trim().TrimStart('\uFEFF');
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
