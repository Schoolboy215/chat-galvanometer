using System;
using System.Diagnostics;
using System.IO.Ports;

namespace ChatGalvanometer
{
    public class SerialCommunicator
    {
        private SerialPort _serialPort;

        public SerialCommunicator(string portName, int baudRate = 115200)
        {
            _serialPort = new SerialPort(portName, baudRate);
            _serialPort.WriteTimeout = 300;
            _serialPort.Open();
        }

        public void SendDecimal(decimal number)
        {
            try
            {
                string message = number.ToString("G") + "d"; // Convert decimal to string
                _serialPort.Write(message); // Send over serial
                _serialPort.BaseStream.Flush();
                Thread.Sleep(50);
            }
            catch
            {
                Trace.WriteLine("Serial timeout");
            }
        }

        public void Close()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }
    }
}
