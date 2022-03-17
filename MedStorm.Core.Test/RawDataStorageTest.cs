using Microsoft.VisualStudio.TestTools.UnitTesting;
using PSSApplication.Core;

namespace MedStorm.Core.Test
{
    [TestClass]
    public class RawDataStorageTest
    {
        public RawDataStorageTest()
        {

        }
        [TestInitialize]
        public void RawDataStorageTest_Init()
        {

        }
        [TestMethod]
        public void RawDataStorage_InsertDataPackageTest()
        {
            RawDataStorage dataStorage = new RawDataStorage();
            MeasurementEventArgs mockData = MockData.GenerateMockData();
            dataStorage.InsertDataPackage(mockData.Measurement);
        }
    }
}