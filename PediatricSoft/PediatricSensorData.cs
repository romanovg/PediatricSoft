﻿using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using LiveCharts;
using LiveCharts.Wpf;
using System.Diagnostics;
using System.ComponentModel;

namespace PediatricSoft
{
    public sealed class PediatricSensorData : INotifyPropertyChanged
    {

        private static readonly PediatricSensorData instance = new PediatricSensorData();

        static PediatricSensorData()
        {
        }

        private PediatricSensorData()
        {
        }

        public static PediatricSensorData Instance
        {
            get
            {
                return instance;
            }
        }

        public static PediatricSensorData GetInstance() { return instance; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        // Constants
        public const bool IsDebugEnabled = false;
        public const bool IsLaserLockDebugEnabled = true;
        public const int NumberOfThreads = 128;
        public const int DataQueueLength = 5000; // number of data points to hold in memory and plot
        public const int DataQueueRunningAvgLength = 10; // number of data points for the running average
        public const int PlotQueueLength = 500;
        public const int UIUpdateInterval = 250; // Update UI every X ms
        public const string DefaultFolder = "Data";
        public const string ValidIDN = "12";

        public const int SerialPortBaudRate = 115200;
        public const int SerialPortWriteTimeout = 1000;
        public const int SerialPortReadTimeout = 1000;
        public const int SerialPortSleepTime = 1000;
        public const int SerialPortShutDownLoopDelay = 100;
        public const int SerialPortStreamBlockSize = 4096; // 4 KiB = 32768 bits = ~284 ms at full 115200 baud
        public const int SerialPortStreamSleepMin = 1;
        public const int SerialPortStreamSleepMax = 1;
        public const int SerialPortErrorCountMax = 10;

        public const byte StartDataFrameByte = 0x02;
        public const byte StopDataFrameByte = 0x03;
        public const byte StartInfoFrameByte = 0x3F;
        public const byte FrameEscapeByte = 0x10;

        public const int ProcessingBufferSize = 1048576; // 1 MiB
        public const int DataBlockSize = 8; // Size of the data block in bytes. 8 bytes = 2 x 32 bit
        public const int InfoBlockSize = 4;

        public const int StateHandlerSleepTime = 10; // in ms
        public const int StateHandlerCellHeatInitialTime = 5000; // 5 seconds
        public const int StateHandlerLaserHeatSweepTime = 1000; // 1 second
        public const int StateHandlerADCColdDelay = 1000; // 1 second

        public const double SensorTargetLaserTransmissionSweep = 0.2;
        public const double SensorTargetLaserTransmissionStep = 0.5;

        public const double SensorADCRawToVolts = (double) 5 / 125 / 16777215;
        public const double SensorADCColdValueLowGainMinVolts = 0.5; // Minimum ADC voltage on low gain

        public const double SensorCoilsCalibrationTeslaPerHex = 21e-12; // 21 pT per step

        public const int MaxNumberOfLaserLockSweepCycles = 30;
        public const int MaxNumberOfLaserLockStepCycles = 3;
        public const int NumberOfFieldZeroingSteps = 1000;
        public const int NumberOfMagnetometerCalibrationSteps = 1000;

        public const int SensorLaserHeatStepCycleDelay = 2000;
        public const int SensorLaserHeatStepSleepTime = 100;

        public const string SensorCommandLaserlock = "@0";
        public const ushort SensorLaserlockDisable = 0x0000;
        public const ushort SensorLaserlockEnable = 0x0005;

        public const string SensorCommandLaserCurrent = "@3";
        public const ushort SensorColdLaserCurrent = 0x0000;
        public const ushort SensorDefaultLaserCurrent = 0x9000;

        public const string SensorCommandLaserCurrentMod = "@4";
        public const ushort SensorLaserCurrentModValue = 0x0250;

        public const string SensorCommandLaserHeat = "@5";
        public const ushort SensorColdLaserHeat = 0x0000;
        public const ushort SensorDefaultLaserHeat = 0x0500;
        public const ushort SensorMinLaserHeat = 0x0000;
        public const ushort SensorMaxLaserHeat = 0x2000;
        public const ushort SensorLaserHeatStep = 10;

        public const string SensorCommandFieldXOffset = "@7";
        public const ushort SensorColdFieldXOffset = 0x8000;

        public const string SensorCommandFieldXAmplitude = "@8";
        public const ushort SensorColdFieldXAmplitude = 0x0000;

        public const string SensorCommandFieldYOffset = "@9";
        public const ushort SensorColdFieldYOffset = 0x8000;

        public const string SensorCommandFieldYAmplitude = "@A";
        public const ushort SensorColdFieldYAmplitude = 0x0000;

        public const string SensorCommandFieldZOffset = "@B";
        public const ushort SensorColdFieldZOffset = 0x8000;
        public const ushort SensorLaserLockFieldZOffset = 0x0000;

        public const string SensorCommandFieldZAmplitude = "@C";
        public const ushort SensorColdFieldZAmplitude = 0x0000;
        public const ushort SensorRunFieldZAmplitude = 0x07C0;

        public const ushort SensorFieldCheckRange = 0x002F; // about 1 nT assuming 21 pT per hex step
        public const ushort SensorFieldStep = 0x000A; // about 0.2 nT assuming 21 pT per hex step

        public const string SensorCommandDigitalDataStreamingAndGain = "@20";
        public const ushort SensorDigitalDataStreamingOffGainLow = 0x0000;
        public const ushort SensorDigitalDataStreamingOffGainHigh = 0x0001;
        public const ushort SensorDigitalDataStreamingOnGainLow = 0x0002;
        public const ushort SensorDigitalDataStreamingOnGainHigh = 0x0003;

        public const string SensorCommandCellHeat = "@21";
        public const ushort SensorColdCellHeat = 0x0000;
        public const ushort SensorRunCellHeat = 0x5000;
        public const ushort SensorMinCellHeat = 0x0000;
        public const ushort SensorMaxCellHeat = 0x9000;
        public const ushort SensorCellHeatStep = 10;

        public const string SensorCommandDigitalDataSelector = "@22";
        public const ushort SensorDigitalDataSelectorADC = 0x0000;
        public const ushort SensorDigitalDataSelectorZDemod = 0x0005;

        public const string SensorCommandGetOptimalLaserCurrent = "@2C";
        public const string SensorCommandGetOptimalBzMod = "@2D";
        public const string SensorCommandGetOptimalCellHeatLockPoint = "@2E";
        public const string SensorCommandGetMaxCellHeat = "@2F"; // Not used. Set to 9000

        public const byte SensorStateInit = 0;
        public const byte SensorStateValid = 1;
        public const byte SensorStateGetOptimalParameters = 2;
        public const byte SensorStateMakeCold = 3;
        public const byte SensorStateCold = 4;
        public const byte SensorStateLockStart = 5;
        public const byte SensorStateLaserLockSweep = 6;
        public const byte SensorStateLaserLockStep = 7;
        public const byte SensorStateLaserLockPID = 8;
        public const byte SensorStateStabilizeCellHeat = 9;
        public const byte SensorStateZeroFields = 10;
        public const byte SensorStateCalibrateMagnetometer = 11;
        public const byte SensorStateIdle = 12;
        public const byte SensorStateStart = 13;
        public const byte SensorStateRun = 14;
        public const byte SensorStateStop = 15;
        public const byte SensorStateFailed = 254;
        public const byte SensorStateShutDown = 255;

        // Globals
        public bool IsPlotting = false;
        public bool PlotWindowClosed = false;
        public bool DebugMode { get; set; } = false;
        public bool SaveDataEnabled { get; set; } = false;
        public bool SaveRAWValues { get; set; } = false;
        public string SaveSuffix { get; set; } = String.Empty;
        public string CommandHistory { get; set; } = String.Empty;

        public ObservableCollection<PediatricSensor> Sensors { get; set; } = new ObservableCollection<PediatricSensor>();
        public string SensorCount { get { return String.Concat("Sensor Count: ", Sensors.Count.ToString()); } }
        public bool IsRunning { get; private set; } = false;
        public List<SensorScanItem> SensorScanList = new List<SensorScanItem>();
        public bool IsScanning { get; private set; } = false;

        public SeriesCollection _SeriesCollection { get; set; } = new SeriesCollection();

        public string dataFolder = String.Empty;




        public void AddAll()
        {
            Parallel.ForEach(SerialPort.GetPortNames(), port =>
             {
                 PediatricSensor sensor = new PediatricSensor(port);
                 sensor.Validate();
                 if (sensor.IsValid)
                     App.Current.Dispatcher.Invoke(() => Sensors.Add(sensor));
                 else
                     while (!sensor.IsDisposed)
                         Thread.Sleep(StateHandlerSleepTime);
                 OnPropertyChanged("SensorCount");
             });

        }

        public void LockAll()
        {
            if (SaveDataEnabled) CreateDataFolder();
            Parallel.ForEach(Sensors, _PediatricSensor =>
            {
                _PediatricSensor.Lock();
            });
        }

        public void StartAll()
        {
            IsRunning = true;
            if (SaveDataEnabled) CreateDataFolder();
            Parallel.ForEach(Sensors, _PediatricSensor =>
            {
                _PediatricSensor.Start();
            });
        }

        public void StopAll()
        {
            IsRunning = false;
            Parallel.ForEach(Sensors, _PediatricSensor =>
            {
                _PediatricSensor.Stop();
            });
        }

        public void ClearAll()
        {
            StopAll();
            Parallel.ForEach(Sensors, _PediatricSensor =>
            {
                _PediatricSensor.Dispose();
                while (!_PediatricSensor.IsDisposed) Thread.Sleep(StateHandlerSleepTime);
            });
            Sensors.Clear();
            OnPropertyChanged("SensorCount");
        }

        public void ZeroFieldsAll()
        {
            Parallel.ForEach(Sensors, _PediatricSensor =>
            {
                _PediatricSensor.ZeroFields();
            });
        }

        public void ValidateSuffixString()
        {
            SaveSuffix = Regex.Replace(SaveSuffix, @"[^\w]", "");
        }

        private void CreateDataFolder()
        {
            if (String.IsNullOrEmpty(SaveSuffix))
                dataFolder = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(),
                DefaultFolder,
                DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
            else
                dataFolder = System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(),
                    DefaultFolder,
                    String.Concat(DateTime.Now.ToString("yyyy-MM-dd_HHmmss"), "_", SaveSuffix));
            System.IO.Directory.CreateDirectory(dataFolder);
        }

    }
}