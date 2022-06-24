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
    public struct DataComment
    {
        public string Comment { get; set; }
    }

    public struct PatientNote
    {
        public string PatientID { get; set; }
    }

    public class RawDataStorage
    {
        StreamWriter m_outputStream = null;
        static object m_lockKey = new object();
        string m_fullRawFileName = ""; 
        string m_fileName = "";

        public RawDataStorage()
        {

        }

        public void DeleteRawDataFile()
        {
            if (m_outputStream != null)
            {
                m_outputStream.Close();
                File.Delete(m_fullRawFileName);
                m_outputStream = null;
            }
        }

        public void SaveRawDataFile(string patientId, bool makeExelFile = true)
        {
            if (m_outputStream != null)
            {
                m_outputStream.Close();
                string xlsxFileName = RawDataToExcel.ExportRawDataToExcel(m_fullRawFileName, patientId);
                if (!string.IsNullOrEmpty(patientId))
                {
                    string targetPath = GetTargetPath();
                    string rawFileNameWithPatientId = Path.Combine(targetPath, patientId + "_" + m_fileName);
                    File.Move(xlsxFileName, rawFileNameWithPatientId.Replace(".txt", ".xlsx"));
                    File.Move(m_fullRawFileName, rawFileNameWithPatientId);
                }

                m_outputStream = null;
            }
        }
        public void CreateRawDataFile(string patientId = null)
        {
            try
            {
                DateTime currentDateTime = DateTime.Now;
                m_fileName = currentDateTime.ToString("HH_mm_ss___dd_MM_yyyy") + "_PainData.txt";

                // Get the Application directory
                string targetPath = GetTargetPath();
                m_fullRawFileName = Path.Combine(targetPath, m_fileName);

                // Make the directry if it dosn't excist
                Directory.CreateDirectory(Path.GetDirectoryName(m_fullRawFileName));
                m_outputStream = new StreamWriter(m_fullRawFileName);
                m_outputStream.AutoFlush = true;

                // Add patienId if it wasn't already registered
                if (!string.IsNullOrEmpty(patientId))
                    UpdatePatientId(patientId);
            }
            catch (Exception ex)
            {
                Log.Error($"CreateRawDataFile failed= {ex.Message}");
            }
        }

        private static string GetTargetPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PSS Application");
        }

        public void InsertDataPackage(BLEMeasurement data)
        {
            lock (m_lockKey)
            {
                try
                {
                    var header = new RawDataHeader { Time = DateTime.Now, DataType = DataType.RawData };
                    string jsonHeader = JsonSerializer.Serialize(header);
                    string jsonString = JsonSerializer.Serialize(data);

                    m_outputStream?.Write($"{jsonHeader}\n");
                    m_outputStream?.Write($"{jsonString}\n");
                }
                catch (Exception ex)
                {
                    Log.Error($"RawDataStorage.InsertDataPackage failed= {ex.Message}");
                }
            }
        }

        public void AddComment(DateTime timestamp, string comment)
        {
            lock (m_lockKey)
            {
                var header = new RawDataHeader { Time = DateTime.Now, DataType = DataType.Comment };
                string jsonHeader = JsonSerializer.Serialize(header);
                var jsonComment = JsonSerializer.Serialize(new DataComment { Comment = comment });

                m_outputStream?.Write($"{jsonHeader}\n");
                m_outputStream?.Write($"{jsonComment}\n");
            }
        }

        public void UpdatePatientId(string patientId)
        {
            lock (m_lockKey)
            {
                var header = new RawDataHeader { Time = DateTime.Now, DataType = DataType.PatienID };
                string jsonHeader = JsonSerializer.Serialize(header);
                var jsonPatientNote = JsonSerializer.Serialize(new PatientNote { PatientID = patientId });

                m_outputStream?.Write($"{jsonHeader}\n");
                m_outputStream?.Write($"{jsonPatientNote}\n");
            }
        }
    }
}
