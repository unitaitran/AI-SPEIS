using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;

namespace ai_speis_be.Services.CodingService.Helpers
{
    public static class CodingExcelParser
    {
        private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace OfficeRelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static async Task<List<Dictionary<string, string>>> ParseExcelAsync(
            IFormFile file,
            string[] expectedColumns,
            CancellationToken cancellationToken = default)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: false);

            var worksheetEntry = GetFirstWorksheetEntry(archive) 
                ?? throw new InvalidDataException("Workbook không chứa worksheet.");

            var sharedStrings = ReadSharedStrings(archive);

            using var worksheetStream = worksheetEntry.Open();
            var worksheet = XDocument.Load(worksheetStream);

            return ReadWorksheetRows(worksheet, sharedStrings, expectedColumns);
        }

        private static ZipArchiveEntry? GetFirstWorksheetEntry(ZipArchive archive)
        {
            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

            if (workbookEntry != null && relationshipsEntry != null)
            {
                using var workbookStream = workbookEntry.Open();
                var workbook = XDocument.Load(workbookStream);

                var firstSheet = workbook.Descendants(SpreadsheetNamespace + "sheet").FirstOrDefault();
                var relationshipId = firstSheet?.Attribute(OfficeRelationshipNamespace + "id")?.Value;

                if (!string.IsNullOrWhiteSpace(relationshipId))
                {
                    using var relationshipsStream = relationshipsEntry.Open();
                    var relationships = XDocument.Load(relationshipsStream);

                    var target = relationships
                        .Descendants(PackageRelationshipNamespace + "Relationship")
                        .FirstOrDefault(r => r.Attribute("Id")?.Value == relationshipId)?
                        .Attribute("Target")?.Value;

                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        target = target.Replace('\\', '/');
                        if (target.StartsWith("/")) target = target.TrimStart('/');
                        else
                        {
                            var parts = new Stack<string>();
                            foreach (var part in $"xl/{target}".Split('/', StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (part == ".") continue;
                                if (part == "..") { if (parts.Count > 0) parts.Pop(); continue; }
                                parts.Push(part);
                            }
                            target = string.Join("/", parts.Reverse());
                        }

                        var entry = archive.GetEntry(target);
                        if (entry != null) return entry;
                    }
                }
            }

            return archive.GetEntry("xl/worksheets/sheet1.xml");
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return new List<string>();

            using var stream = entry.Open();
            var sharedStrings = XDocument.Load(stream);

            return sharedStrings
                .Descendants(SpreadsheetNamespace + "si")
                .Select(si => string.Concat(si.Descendants(SpreadsheetNamespace + "t").Select(t => t.Value)))
                .ToList();
        }

        private static List<Dictionary<string, string>> ReadWorksheetRows(
            XDocument worksheet, 
            List<string> sharedStrings,
            string[] expectedColumns)
        {
            var rowElements = worksheet.Descendants(SpreadsheetNamespace + "row").ToList();
            if (rowElements.Count == 0) throw new InvalidDataException("File Excel không có dữ liệu.");

            Dictionary<string, string>? headerColumns = null;
            var dataRows = new List<Dictionary<string, string>>();

            var expectedSet = new HashSet<string>(expectedColumns, StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < rowElements.Count; index++)
            {
                var rowElement = rowElements[index];
                var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var nextColumnIndex = 1;

                foreach (var cell in rowElement.Elements(SpreadsheetNamespace + "c"))
                {
                    var reference = cell.Attribute("r")?.Value;
                    var columnName = string.IsNullOrWhiteSpace(reference)
                        ? GetColumnName(nextColumnIndex)
                        : ExtractColumnName(reference);

                    cells[columnName] = ReadCellValue(cell, sharedStrings).Trim();
                    nextColumnIndex = GetColumnIndex(columnName) + 1;
                }

                if (cells.Values.All(string.IsNullOrWhiteSpace)) continue;

                if (headerColumns == null)
                {
                    headerColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var cell in cells)
                    {
                        var header = cell.Value.Trim().ToLowerInvariant();
                        if (string.IsNullOrWhiteSpace(header)) continue;
                        headerColumns[header] = cell.Key; // maps lowercase header to e.g. "A", "B", "C"
                    }
                    continue;
                }

                var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var expectedCol in expectedSet)
                {
                    var colLower = expectedCol.ToLowerInvariant();
                    if (headerColumns.TryGetValue(colLower, out var sourceColumn) && cells.TryGetValue(sourceColumn, out var value))
                    {
                        rowData[expectedCol] = value;
                    }
                    else
                    {
                        rowData[expectedCol] = string.Empty;
                    }
                }

                if (rowData.Values.All(string.IsNullOrWhiteSpace)) continue;
                dataRows.Add(rowData);
            }

            if (headerColumns == null) throw new InvalidDataException("Không tìm thấy dòng tiêu đề trong file.");

            return dataRows;
        }

        private static string ReadCellValue(XElement cell, List<string> sharedStrings)
        {
            var dataType = cell.Attribute("t")?.Value;

            if (dataType == "inlineStr")
            {
                return string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(t => t.Value));
            }

            var value = cell.Element(SpreadsheetNamespace + "v")?.Value;

            if (dataType == "s" && int.TryParse(value, out var sharedStringIndex) && sharedStringIndex >= 0 && sharedStringIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedStringIndex];
            }

            if (dataType == "b") return value == "1" ? "TRUE" : "FALSE";

            return value ?? string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(t => t.Value));
        }

        private static string ExtractColumnName(string cellReference)
        {
            return new string(cellReference.TakeWhile(char.IsLetter).Select(char.ToUpperInvariant).ToArray());
        }

        private static int GetColumnIndex(string columnName)
        {
            var columnIndex = 0;
            foreach (var letter in columnName)
            {
                columnIndex *= 26;
                columnIndex += char.ToUpperInvariant(letter) - 'A' + 1;
            }
            return columnIndex;
        }

        private static string GetColumnName(int columnIndex)
        {
            var columnName = string.Empty;
            while (columnIndex > 0)
            {
                var modulo = (columnIndex - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                columnIndex = (columnIndex - modulo) / 26;
            }
            return columnName;
        }
    }
}
