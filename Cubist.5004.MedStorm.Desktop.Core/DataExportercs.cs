using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.SignalR;
using Serilog;

public struct DataExportObject
{
    public string Timestamp { get; set; }
    public int Pain { get; set; }
    public int Awakening { get; set; }
    public int Nerveblock { get; set; }
    public int BadSignal { get; set; }
    public double[] SkinCond { get; set; }
    public DataExportObject(string timestamp, int pain, int awakening, int nerveblock, int badSignal, double[] skinCond)
    {
        Timestamp = timestamp;
        Pain = pain;
        Awakening = awakening;
        Nerveblock = nerveblock;
        BadSignal = badSignal;
        SkinCond = skinCond;
    }
}
public class DataExporter : Hub
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
                                 new Column("Comment", "L")
    };

    private static SpreadsheetDocument m_spreadSheet;

    private static string m_fileName;
    private static string m_tempPath;
    private static string m_sheetName = "Measurements";
    private static uint m_currentRow;
    private static object m_lockKey = new object();

    protected static IHubContext<DataExporter> m_hubContext;

    public DataExporter(IHubContext<DataExporter> hubContext)
    {
        m_hubContext = hubContext;
    }

    public static void CreateExcelFile()
    {
        m_currentRow = 3;
        m_fileName = getFileName();
        m_tempPath = Path.GetTempPath();
        Log.Information($"DataExporter.CreateExcelFile: temp-path={m_tempPath}, fileName{m_fileName}");

        m_spreadSheet = SpreadsheetDocument.Create(Path.Combine(m_tempPath, m_fileName), SpreadsheetDocumentType.Workbook);

        WorkbookPart workbookpart = m_spreadSheet.AddWorkbookPart();
        workbookpart.Workbook = new Workbook();

        WorksheetPart worksheetPart = workbookpart.AddNewPart<WorksheetPart>();
        worksheetPart.Worksheet = new Worksheet(new SheetData());

        Sheets sheets = m_spreadSheet.WorkbookPart.Workbook.
        AppendChild<Sheets>(new Sheets());

        Sheet sheet = new Sheet()
        {
            Id = m_spreadSheet.WorkbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = m_sheetName
        };
        sheets.Append(sheet);

        CreateColumns();
        workbookpart.Workbook.Save();
    }
    public static void AddData(DataExportObject dataExportObject)
    {
        if (m_spreadSheet != null)
            Task.Run(() => InsertDataPackage(dataExportObject));
    }
    private static void CreateColumns()
    {
        WorksheetPart worksheetPart = m_spreadSheet.WorkbookPart.WorksheetParts.First();
        InsertCell("A", 1, new CellValue("Patient Id"), new EnumValue<CellValues>(CellValues.String), worksheetPart);
        foreach (Column column in m_columns)
        {
            InsertCell(column.Cell, m_currentRow, new CellValue(column.Text), new EnumValue<CellValues>(CellValues.String), worksheetPart);
        }

        worksheetPart.Worksheet.Save();
        m_currentRow += 1;
    }
    private static void InsertCell(string columnName, uint rowIndex, CellValue value, EnumValue<CellValues> dataType, WorksheetPart worksheetPart)
    {
        Worksheet worksheet = worksheetPart.Worksheet;
        SheetData sheetData = worksheet.GetFirstChild<SheetData>();
        string cellReference = columnName + rowIndex;

        Row row = sheetData.Elements<Row>().Where(r => r.RowIndex == rowIndex).FirstOrDefault();
        if (row == default(Row))
        {
            row = new Row() { RowIndex = rowIndex };
            sheetData.Append(row);
        }

        Cell refCell = row.Descendants<Cell>().LastOrDefault();

        Cell newCell = new Cell() { CellReference = cellReference };
        row.InsertAfter(newCell, refCell);

        newCell.CellValue = value;
        newCell.DataType = dataType;

        worksheet.Save();
    }

    private static void InsertDataPackage(DataExportObject obj)
    {
        lock (m_lockKey)
        {
            WorksheetPart worksheetPart = m_spreadSheet.WorkbookPart.WorksheetParts.First();

            InsertCell("A", m_currentRow, new CellValue(obj.Timestamp), CellValues.String, worksheetPart);
            InsertCell("B", m_currentRow, new CellValue(obj.Pain), CellValues.Number, worksheetPart);
            InsertCell("C", m_currentRow, new CellValue(obj.Awakening), CellValues.Number, worksheetPart);
            InsertCell("D", m_currentRow, new CellValue(obj.Nerveblock), CellValues.Number, worksheetPart);
            InsertCell("E", m_currentRow, new CellValue(obj.BadSignal == 0 ? "False" : "True"), CellValues.String, worksheetPart);

            InsertCell("F", m_currentRow, new CellValue(Math.Round(obj.SkinCond[0], 3)), CellValues.String, worksheetPart);
            InsertCell("G", m_currentRow, new CellValue(Math.Round(obj.SkinCond[1], 3)), CellValues.String, worksheetPart);
            InsertCell("H", m_currentRow, new CellValue(Math.Round(obj.SkinCond[2], 3)), CellValues.String, worksheetPart);
            InsertCell("I", m_currentRow, new CellValue(Math.Round(obj.SkinCond[3], 3)), CellValues.String, worksheetPart);
            InsertCell("J", m_currentRow, new CellValue(Math.Round(obj.SkinCond[4], 3)), CellValues.String, worksheetPart);

            m_currentRow += 1;
            worksheetPart.Worksheet.Save();
        }
    }
    private static void UpdateCell(string columnName, uint rowIndex, CellValue value, WorksheetPart worksheetPart)
    {
        Worksheet worksheet = worksheetPart.Worksheet;
        SheetData sheetData = worksheet.GetFirstChild<SheetData>();

        Row row = sheetData.Elements<Row>().Where(r => r.RowIndex == rowIndex).FirstOrDefault();
        if (row != default(Row))
        {
            Cell cell = row.Elements<Cell>().Where(c => string.Compare(c.CellReference.Value, columnName + rowIndex, true) == 0).First();
            cell.CellValue = value;
        }
        worksheetPart.Worksheet.Save();
    }
    public static void SaveFile(string patientId)
    {
        Log.Information($"DataExporter.SaveFile: Update patiend ID and Save");
        DataExporter.UpdatePatientId(patientId);
        m_spreadSheet.Close();
        m_spreadSheet = null;   // To stop unwated updates to the spredsheets

        // Make a copy of the spredsheet to PSS Application directory
        string targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PSS Application");
        string targetFile = Path.Combine(targetPath, m_fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(targetFile));

        File.Copy(Path.Combine(m_tempPath, m_fileName), targetFile);
        DeleteTempFile();
        Log.Information($"DataExporter.SaveFile: Saved file with path={targetPath}, fileName{m_fileName}");
    }

    public static void DeleteTempFile()
    {
        if (m_tempPath != null && m_fileName != null)
        {
            string tempFile = Path.Combine(m_tempPath, m_fileName);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
    public static void AddComment(DateTime timestamp, string comment)
    {
        //using (SpreadsheetDocument spreadSheet = SpreadsheetDocument.Open(Path.Combine(m_tempPath, m_fileName), true))
        //{
        string timestring = timestamp.ToShortTimeString();
        lock (m_lockKey)
        {
            int row = getLastRowNo();   // getCommentRow(timestamp);
            WorksheetPart worksheetPart = m_spreadSheet.WorkbookPart.WorksheetParts.First();

            InsertCell("K", (uint)row, new CellValue(timestring), new EnumValue<CellValues>(CellValues.String), worksheetPart);
            InsertCell("L", (uint)row, new CellValue(comment), new EnumValue<CellValues>(CellValues.String), worksheetPart);
        }
    }
    public static void UpdatePatientId(string patientId)
    {
        //using (SpreadsheetDocument spreadSheet = m_spreadSheet.Open(Path.Combine(m_tempPath, m_fileName), true))
        //{
        WorksheetPart worksheetPart = m_spreadSheet.WorkbookPart.WorksheetParts.First();

        UpdateCell("A", 1, new CellValue($"Patient Id: {patientId}"), worksheetPart);

        worksheetPart.Worksheet.Save();
        //}
    }

    public async Task AlertFilePath(string path)
    {
        if (m_hubContext != null && m_hubContext.Clients != null)
        {
            await DataExporter.m_hubContext.Clients.All.SendAsync("AlertFilePath", path);
        }
    }

    private static string getFileName()
    {
        DateTime currentDateTime = DateTime.UtcNow;
        string fileName = "measurements_" + currentDateTime.ToLocalTime().ToString("yyMMdd_HHmmss") + ".xlsx";

        return fileName;
    }

    private static int getLastRowNo()
    {       
        WorkbookPart workbookPart = m_spreadSheet.WorkbookPart;
        Sheet sheet = workbookPart.Workbook.Descendants<Sheet>().Where(s => s.Name == m_sheetName).FirstOrDefault();
        WorksheetPart worksheetPart = (WorksheetPart)(workbookPart.GetPartById(sheet.Id));

        var rowList = worksheetPart.Worksheet.Descendants<Row>();
        if (rowList != null && rowList.Count() > 0)
            return rowList.Count();
        else
            return 5;
    }
}

