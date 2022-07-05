using Microsoft.AspNetCore.SignalR;
using PSSApplication.Common;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PSSApplication.Core
{
    public class MockData
    {
        private static System.Timers.Timer timerMock;

        public event EventHandler<MeasurementEventArgs> NewMeasurement;

        public void StartListningForPainSensors()
        {
            startTimerMock();
        }

        public void StopListningForPainSensors()
        {
            if (timerMock == null) return;
            timerMock.Elapsed -= timerMock_Tick;
            timerMock.Interval = 1000; // one second
            timerMock.Stop();
            timerMock = null;
        }

        public async Task<bool> IsPaired()
        {
            return timerMock != null;
        }

        public void CloseApplication()
        {
            if (timerMock != null)
            {
                timerMock.Elapsed -= timerMock_Tick;
                timerMock.Stop();
                timerMock = null;
            }
        }

        public static MeasurementEventArgs GenerateMockData()
        {
            var random = new Random();
            byte pps = (byte)random.Next(0, 11); //PPS is an int in range 0-10
            byte area = (byte)random.Next(0, 101); //AUC is an int in range 0-100
            float meanRiseTime = (float)random.NextDouble() * 2f - 1f; //MRT is a float in range -1 to 1
            byte nerveBlock = (byte)random.Next(0, 11); //NBV is an int in range 0-10
            byte badSignal = (byte)random.Next(0, 2);

            DateTime currentDateTime = DateTime.UtcNow;

            // SC are floats between 0 and 200 in array of 5 elements
            var skinArr = Enumerable.Repeat(0, 5)
                .Select(x => random.NextDouble() + (double)random.Next(0, 200)).ToArray();

            return new MeasurementEventArgs(pps, area, nerveBlock, skinArr, badSignal, meanRiseTime);
        }

        private void SendMockMessageToClient()
        {
            MeasurementEventArgs args= GenerateMockData();
             NewMeasurement?.Invoke(this, args);
        }

        private void startTimerMock()
        {
            timerMock = new System.Timers.Timer();
            timerMock.Elapsed += timerMock_Tick;
            timerMock.Interval = 1000; // one second
            timerMock.Start();
        }

        private void timerMock_Tick(object sender, EventArgs e)
        {
            SendMockMessageToClient();
        }
    }
}
