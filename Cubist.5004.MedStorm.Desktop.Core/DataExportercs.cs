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

public class DataExportObject
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

    private static Column[] columns = { new Column("Time", "A"), 
                                 new Column("Pain-Nociceptive", "B"), 
                                 new Column("Awakening", "C"), 
                                 new Column("Nerve Block", "D"),
                                 new Column("Bad signal", "E"),
                                 new Column("Skin Conductivity 1", "F"),
                                 new Column("Skin Conductivity 2", "G"),
                                 new Column("Skin Conductivity 3", "H"),
                                 new Column("Skin Conductivity 4", "I"),
                                 new Column("Skin Conductivity 5", "J"),
                                 new Column("Comment", "K") 
    };

    private static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static WorkbookPart workbookpart;
    private static SpreadsheetDocument spreadsheetDocument;

    private static string fileName;
    private static string tempPath;
    private static string sheetName = "Measurements";

    private static uint currentRow;

    private static List<DataExportObject> dataExportPackage = new List<DataExportObject>();
    private static readonly int DataExportPackageLength = 10;

    protected IHubContext<DataExporter> _context;

    public DataExporter(IHubContext<DataExporter> context)
    {
        _context = context;
    }

    public static void CreateExcelFile()
    {
        currentRow = 3;
        fileName = getFileName();
        tempPath = Path.GetTempPath();

        spreadsheetDocument = SpreadsheetDocument.Create(Path.Combine(tempPath, fileName), SpreadsheetDocumentType.Workbook);
        
        workbookpart = spreadsheetDocument.AddWorkbookPart();
        workbookpart.Workbook = new Workbook();
        
        WorksheetPart worksheetPart = workbookpart.AddNewPart<WorksheetPart>();
        worksheetPart.Worksheet = new Worksheet(new SheetData());
        
        Sheets sheets = spreadsheetDocument.WorkbookPart.Workbook.
        AppendChild<Sheets>(new Sheets());
        
        Sheet sheet = new Sheet()
        {
            Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = sheetName
        };
        sheets.Append(sheet);

        workbookpart.Workbook.Save();
        spreadsheetDocument.Close();

        CreateColumns();
    }
    public static void AddData(DataExportObject dataExportObject)
    {
        dataExportPackage.Add(dataExportObject);

        if ( dataExportPackage.Count == DataExportPackageLength )
        {
            List<DataExportObject> dataExportPackageCopy = new List<DataExportObject>(dataExportPackage);
            dataExportPackage.Clear();
            InsertDataPackage(dataExportPackageCopy);
        }
    }
    private static void CreateColumns()
    {
        using (SpreadsheetDocument spreadSheet = SpreadsheetDocument.Open(Path.Combine(tempPath, fileName), true))
        {
            WorksheetPart worksheetPart = spreadSheet.WorkbookPart.WorksheetParts.First();

            InsertCell("A", 1, new CellValue("Patient Id"), new EnumValue<CellValues>(CellValues.String), worksheetPart);

            foreach (Column column in columns)
            {
                InsertCell(column.Cell, currentRow, new CellValue(column.Text), new EnumValue<CellValues>(CellValues.String), worksheetPart);
            }

            worksheetPart.Worksheet.Save();
        }

        currentRow += 1;
    }
    private static void InsertCell(string columnName, uint rowIndex, CellValue value, EnumValue<CellValues> dataType, WorksheetPart worksheetPart)
    {
        Worksheet worksheet = worksheetPart.Worksheet;
        SheetData sheetData = worksheet.GetFirstChild<SheetData>();
        string cellReference = columnName + rowIndex;

        Row row;
        if (sheetData.Elements<Row>().Where(r => r.RowIndex == rowIndex).Count() != 0)
        {
            row = sheetData.Elements<Row>().Where(r => r.RowIndex == rowIndex).First();
        }
        else
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
    private static void InsertDataPackage(List<DataExportObject> dataPackage)
    {
        using (SpreadsheetDocument spreadSheet = SpreadsheetDocument.Open(Path.Combine(tempPath, fileName), true))
        {
            WorksheetPart worksheetPart = spreadSheet.WorkbookPart.WorksheetParts.First();

            foreach (DataExportObject obj in dataPackage.ToList())
            {
                InsertCell("A", currentRow, new CellValue(obj.Timestamp), new EnumValue<CellValues>(CellValues.String), worksheetPart);
                InsertCell("B", currentRow, new CellValue(obj.Pain), new EnumValue<CellValues>(CellValues.Number), worksheetPart);
                InsertCell("C", currentRow, new CellValue(obj.Awakening), new EnumValue<CellValues>(CellValues.Number), worksheetPart);
                InsertCell("D", currentRow, new CellValue(obj.Nerveblock), new EnumValue<CellValues>(CellValues.Number), worksheetPart);
                InsertCell("E", currentRow, new CellValue(getBadSignalString(obj.BadSignal)), new EnumValue<CellValues>(CellValues.String), worksheetPart);

                InsertCell("F", currentRow, new CellValue(Math.Round(obj.SkinCond[0], 3)), new EnumValue<CellValues>(CellValues.String), worksheetPart);
                InsertCell("G", currentRow, new CellValue(Math.Round(obj.SkinCond[1], 3)), new EnumValue<CellValues>(CellValues.String), worksheetPart);
                InsertCell("H", currentRow, new CellValue(Math.Round(obj.SkinCond[2], 3)), new EnumValue<CellValues>(CellValues.String), worksheetPart);
                InsertCell("I", currentRow, new CellValue(Math.Round(obj.SkinCond[3], 3)), new EnumValue<CellValues>(CellValues.String), worksheetPart);
                InsertCell("J", currentRow, new CellValue(Math.Round(obj.SkinCond[4], 3)), new EnumValue<CellValues>(CellValues.String), worksheetPart);

                currentRow += 1;
            }

            worksheetPart.Worksheet.Save();
        }
    }
    private static void UpdateCell(string columnName, uint rowIndex, CellValue value, WorksheetPart worksheetPart)
    {
        Worksheet worksheet = worksheetPart.Worksheet;
        SheetData sheetData = worksheet.GetFirstChild<SheetData>();

        Row row;
        if (sheetData.Elements<Row>().Where(r => r.RowIndex == rowIndex).Count() != 0)
        {
            row = sheetData.Elements<Row>().Where(r => r.RowIndex == rowIndex).First();
            Cell cell = row.Elements<Cell>().Where(c => string.Compare(c.CellReference.Value, columnName + rowIndex, true) == 0).First();
            cell.CellValue = value;
        }
    }
    public async Task SaveFile(string patientId)
    {
        if (dataExportPackage.Count > 0)
        {
            InsertDataPackage(dataExportPackage);
            dataExportPackage.Clear();
        }

        UpdatePatientId(patientId);

        string targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PSS Application");
        string targetFile = Path.Combine(targetPath, fileName);

        //Create PSS Application directory if it does not already exist
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
        
        File.Copy(Path.Combine(tempPath, fileName), targetFile);
        DeleteTempFile();
        await AlertFilePath(targetFile);
    }
    public void DeleteTempFile()
    {
        string tempFile = Path.Combine(tempPath, fileName);
        File.Delete(tempFile);
    }
    public static void DeleteIfNotAlreadyDeleted()
    {
        if (tempPath != null && fileName != null)
        {
            string tempFile = Path.Combine(tempPath, fileName);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
        
    }
    public void AddComment(double timestamp, string comment)
    {
        using (SpreadsheetDocument spreadSheet = SpreadsheetDocument.Open(Path.Combine(tempPath, fileName), true))
        {
            uint row = getCommentRow(timestamp, spreadSheet);
            string timestring = epoch.AddMilliseconds(timestamp).ToLocalTime().ToString();
            WorksheetPart worksheetPart = spreadSheet.WorkbookPart.WorksheetParts.First();

            InsertCell("F", row, new CellValue(comment), new EnumValue<CellValues>(CellValues.String), worksheetPart);
        }
    }
    public static void UpdatePatientId(string patientId)
    {
        using (SpreadsheetDocument spreadSheet = SpreadsheetDocument.Open(Path.Combine(tempPath, fileName), true))
        {
            WorksheetPart worksheetPart = spreadSheet.WorkbookPart.WorksheetParts.First();

            UpdateCell("A", 1, new CellValue($"Patient Id: {patientId}"), worksheetPart);

            worksheetPart.Worksheet.Save();
        }
    }
    public async Task AlertFilePath(string path)
    {
        if (_context != null && _context.Clients != null)
        {
            await _context.Clients.All.SendAsync("AlertFilePath", path);
        }
    }

    private static string getBadSignalString(int badSignal)
    {
        if (badSignal == 0)
            return "False";

        return "True";
    }

    private static string getFileName()
    {
        DateTime currentDateTime = DateTime.UtcNow;
        string fileName = "measurements_" + currentDateTime.ToLocalTime().ToString("yyMMdd_HHmmss") + ".xlsx";

        return fileName;
    }
    private uint getCommentRow(double timestamp, SpreadsheetDocument spreadSheet)
    {
        uint firstDataRow = 4;
        DateTime firstRowDateTime = getCellValue(spreadSheet, "A" + firstDataRow);
        var commentDateTime = epoch.AddMilliseconds(timestamp).ToLocalTime();

        uint elapsedTime = (uint)((commentDateTime - firstRowDateTime).TotalSeconds);
        uint commentRow = firstDataRow + elapsedTime;

        return commentRow;
    }
    private DateTime getCellValue(SpreadsheetDocument spreadSheet, string cellReference)
    {
        WorkbookPart workbookPart = spreadSheet.WorkbookPart;
        Sheet sheet = workbookPart.Workbook.Descendants<Sheet>().Where(s => s.Name == sheetName).FirstOrDefault();
        WorksheetPart worksheetPart = (WorksheetPart)(workbookPart.GetPartById(sheet.Id));

        Cell cell = worksheetPart.Worksheet.Descendants<Cell>().Where(c => c.CellReference == cellReference).FirstOrDefault();
        var cellValue = DateTime.Parse(cell.InnerText);

        return cellValue;
    }
}

