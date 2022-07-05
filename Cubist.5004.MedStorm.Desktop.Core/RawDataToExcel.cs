using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PSSApplication.Common;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PSSApplication.Core
{
    public static class RawDataToExcel
    {
        private class Column
        {
            public string Text { get; set; }
            public string Cell { get; set; }
            public Column(string text, string cell)
            {
                Text = text;
                Cell = cell;
            }
        }

        private static Column[] m_columns = { new Column("Time", "A"),
                                 new Column("Pain-Nociceptive", "B"),
                                 new Column("Awakening", "C"),
                                 new Column("Nerve Block", "D"),
                                 new Column("Bad signal", "E"),
                                 new Column("Skin Conductivity 1", "F"),
                                 new Column("Skin Conductivity 2", "G"),
                                 new Column("Skin Conductivity 3", "H"),
                                 new Column("Skin Conductivity 4", "I"),
                                 new Column("Skin Conductivity 5", "J"),
                                 new Column("Comment Time", "K"),
                                 new Column("Comment", "L")};

        static SpreadsheetDocument m_spreadSheet;
        static uint m_currentRow = 0;
        static WorkbookPart m_workbookpart;
        static WorksheetPart m_worksheetPart;
        static Worksheet m_worksheet;
        static SheetData m_sheetData;
        static Sheet m_sheet;

        public static string ExportRawDataToExcel(string fileName, string patientId)
        {
            m_currentRow = 3;
            string outFileName = fileName.Replace(".txt", ".xlsx");
            Log.Information($"RawDataToExcel.ExportRowDataToExcel: fileName={fileName}");

            m_spreadSheet = SpreadsheetDocument.Create(outFileName, SpreadsheetDocumentType.Workbook);
            m_workbookpart = m_spreadSheet.AddWorkbookPart();
            m_workbookpart.Workbook = new Workbook();
            m_worksheetPart = m_workbookpart.AddNewPart<WorksheetPart>();
            m_sheetData = new SheetData();
            m_worksheet = new Worksheet(m_sheetData);
            m_worksheetPart.Worksheet = m_worksheet;

            Sheets sheets = m_spreadSheet.WorkbookPart.Workbook.
            AppendChild<Sheets>(new Sheets());

            m_sheet = new Sheet()
            {
                Id = m_spreadSheet.WorkbookPart.GetIdOfPart(m_worksheetPart),
                SheetId = 1,
                Name = "Measurements"
            };
            sheets.Append(m_sheet);

            var id = string.IsNullOrEmpty(patientId) ? "" : patientId;
            CreateColumns(id);

            StreamReader reader = new StreamReader(fileName);
            string line = reader.ReadLine();
            while (line != null)
            {
                try
                {
                    RawDataHeader header = JsonSerializer.Deserialize<RawDataHeader>(line);
                    line = reader.ReadLine();
                    if (line == null)
                        break;

                    switch (header.DataType)
                    {
                        case DataType.RawData:
                            BLEMeasurement data = JsonSerializer.Deserialize<BLEMeasurement>(line);
                            InsertDataPackage(header.Time, data);
                            break;
                        case DataType.Comment:
                            DataComment comment = JsonSerializer.Deserialize<DataComment>(line);
                            AddComment(header.Time, comment.Comment);
                            break;
                        case DataType.PatienID:
                            PatientNote note = JsonSerializer.Deserialize<PatientNote>(line);
                            //UpdatePatientId(note.PatientID);
                            break;
                        default:
                            break;
                    }

                }
                catch (Exception)
                {
                    continue;
                }
                finally
                {
                    line = reader.ReadLine();
                }
            }

            m_worksheet.Save();
            m_workbookpart.Workbook.Save();
            m_spreadSheet.Save();
            m_spreadSheet.Close();
            m_spreadSheet = null;
            reader.Close();
            return outFileName;
        }
        private static void CreateColumns(string patientId)
        {
            InsertCell("A", 1, new CellValue($"Patient Id:{patientId}"), new EnumValue<CellValues>(CellValues.String));
            foreach (Column column in m_columns)
            {
                InsertCell(column.Cell, m_currentRow, new CellValue(column.Text), new EnumValue<CellValues>(CellValues.String));
            }
            m_currentRow += 1;
        }
        private static void InsertCell(string columnName, Row row, CellValue value, EnumValue<CellValues> dataType)
        {
            string cellReference = columnName + m_currentRow;
            Cell refCell = row.Descendants<Cell>().LastOrDefault();

            Cell newCell = new Cell() { CellReference = cellReference };
            row.InsertAfter(newCell, refCell);

            newCell.CellValue = value;
            newCell.DataType = dataType;
        }

        private static void InsertCell(string columnName, uint rowIndex, CellValue value, EnumValue<CellValues> dataType)
        {
            string cellReference = columnName + rowIndex;

            Row row = m_sheetData.Elements<Row>().Where(r => r.RowIndex == rowIndex).FirstOrDefault();
            if (row == default(Row))
            {
                row = new Row() { RowIndex = rowIndex };
                m_sheetData.Append(row);
            }

            Cell refCell = row.Descendants<Cell>().LastOrDefault();

            Cell newCell = new Cell() { CellReference = cellReference };
            row.InsertAfter(newCell, refCell);

            newCell.CellValue = value;
            newCell.DataType = dataType;
        }

        private static void InsertDataPackage(DateTime timeStamp, BLEMeasurement data)
        {
            Row row = GetOrAddRow(m_currentRow);

            InsertCell("A", row, new CellValue(timeStamp), CellValues.String);
            InsertCell("B", row, new CellValue(data.PSS), CellValues.Number);
            InsertCell("C", row, new CellValue(data.AUC), CellValues.Number);
            InsertCell("D", row, new CellValue(data.NBV), CellValues.Number);
            InsertCell("E", row, new CellValue(data.BS == 0 ? "False" : "True"), CellValues.String);

            InsertCell("F", row, new CellValue(Math.Round(data.SC[0], 3)), CellValues.String);
            InsertCell("G", row, new CellValue(Math.Round(data.SC[1], 3)), CellValues.String);
            InsertCell("H", row, new CellValue(Math.Round(data.SC[2], 3)), CellValues.String);
            InsertCell("I", row, new CellValue(Math.Round(data.SC[3], 3)), CellValues.String);
            InsertCell("J", row, new CellValue(Math.Round(data.SC[4], 3)), CellValues.String);

            m_currentRow += 1;
        }

        private static Row GetOrAddRow(uint rowIndex)
        {
            Row row = m_sheetData.Elements<Row>().Where(r => r.RowIndex == rowIndex).FirstOrDefault();
            if (row == default(Row))
            {
                row = new Row() { RowIndex = rowIndex };
                m_sheetData.Append(row);
            }

            return row;
        }

        private static void UpdateCell(string columnName, uint rowIndex, CellValue value)
        {
            Row row = m_sheetData.Elements<Row>().Where(r => r.RowIndex == rowIndex).FirstOrDefault();
            if (row != default(Row))
            {
                Cell cell = row.Elements<Cell>().Where(c => string.Compare(c.CellReference.Value, columnName + rowIndex, true) == 0).First();
                cell.CellValue = value;
            }
        }
        public static void AddComment(DateTime timestamp, string comment)
        {
            InsertCell("K", m_currentRow - 1, new CellValue(timestamp), new EnumValue<CellValues>(CellValues.String));
            InsertCell("L", m_currentRow - 1, new CellValue(comment), new EnumValue<CellValues>(CellValues.String));
        }
    }
}
