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
        string m_fullFileRawFileName = "";

        public RawDataStorage()
        {

        }
        public void SaveRawDataFile(string patientId, bool makeExelFile = true)
        {
            if (m_outputStream != null)
            {
                m_outputStream.Close();
                RawDataToExcel.ExportRowDataToExcel(m_fullFileRawFileName, patientId);
                m_outputStream = null;
            }
        }
        public void CreateRawDataFile()
        {
            try
            {
                DateTime currentDateTime = DateTime.Now;
                var fileName = currentDateTime.ToString("HH_mm_ss___dd_MM_yyyy") + "_PainData.txt";

                // Use the PSS Application directory
                string targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PSS Application");
                m_fullFileRawFileName = Path.Combine(targetPath, fileName);

                // Make the directry if it dosn't excist
                Directory.CreateDirectory(Path.GetDirectoryName(m_fullFileRawFileName));
                m_outputStream = new StreamWriter(m_fullFileRawFileName);
                m_outputStream.AutoFlush = true;
            }
            catch (Exception ex)
            {
                Log.Error($"CreateRawDataFile failed= {ex.Message}");
            }
        }

        public void InsertDataPackage(BLEMeasurement data)
        {
            lock (m_lockKey)
            {
                try
                {
                    var header = new RawDataHeader{ Time = DateTime.Now, DataType = DataType.RawData };
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
                var jsonComment= JsonSerializer.Serialize(new DataComment { Comment = comment });

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
