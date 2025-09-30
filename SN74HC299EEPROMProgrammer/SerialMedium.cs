using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SN74HC299EEPROMProgrammer
{
    public class SerialMedium
    {

        public SerialPort serialPort;
        public bool ShowTransferLogs = false;
        public Queue<(UInt32 address, List<byte> data)> writeQueue = new Queue<(UInt32, List<byte>)>();
        public Queue<(UInt32 address, byte length)> readQueue = new Queue<(UInt32, byte)>();
        public Dictionary<UInt32, List<byte>> ReceiveBuffer = new Dictionary<UInt32, List<byte>>();

        string incoming;

        bool uploadModeSet = false;
        bool downloadModeSet = false;

        public void WriteColored(string message, ConsoleColor foreground, ConsoleColor background = ConsoleColor.Black, bool resetAfter = true, bool useOriginalBackground = true, bool writeLine = false)
        {
            var originalForeground = Console.ForegroundColor;
            var originalBackground = Console.BackgroundColor;

            Console.ForegroundColor = foreground;
            if (!useOriginalBackground) Console.BackgroundColor = background;
            if (writeLine) Console.WriteLine(message); else Console.Write(message);

            if (resetAfter)
            {
                Console.ForegroundColor = originalForeground;
                Console.BackgroundColor = originalBackground;
            }
        }
        public AutoResetEvent autoReset = new AutoResetEvent(false);

        public void Begin(string portName, UInt32 baudRate)
        {
            serialPort = new SerialPort("COM5", 921600);
            serialPort.Open();
        }
        public bool Upload(UInt32 startingAddress, List<byte> data, int chunkSize = 16)
        {
            serialPort.Open();
            if (downloadModeSet) { serialPort.DataReceived -= SerialDataReceivedHandler_Download; downloadModeSet = false; }
            if (!uploadModeSet) { serialPort.DataReceived += SerialDataReceivedHandler_Upload; uploadModeSet = true; }

            // Prepare Queue
            UInt32 address = startingAddress;

            for (int i = 0; i < data.Count; i += chunkSize)
            {
                int len = Math.Min(chunkSize, data.Count - i);
                var chunk = data.GetRange(i, len);
                writeQueue.Enqueue((address, chunk));
                address += (UInt32)len;

            }

            // Start the first write
            serialPort.DiscardInBuffer();
            SendNextChunk();

            // block until upload finished
            autoReset.WaitOne();
            serialPort.Close();

            return true;
        }
        public byte[] DownloadSingle(UInt32 startingAddress, byte length)
        {
            serialPort.Open();
            if (!downloadModeSet) { serialPort.DataReceived += SerialDataReceivedHandler_Download; downloadModeSet = true; }
            if (uploadModeSet) { serialPort.DataReceived -= SerialDataReceivedHandler_Upload; uploadModeSet = false; }
            ReceiveBuffer = new Dictionary<UInt32, List<byte>>();
            // Prepare Queue
            UInt32 address = startingAddress;


            readQueue.Enqueue((address, length));



            // Start the first write
            serialPort.DiscardInBuffer();
            ReceiveNextChunk();

            // block until download finished
            autoReset.WaitOne();

            serialPort.Close();
            return ReceiveBuffer.Values.ElementAt(0).ToArray();
        }
        public Dictionary<UInt32, List<byte>> Download(UInt32 startingAddress, int length, int chunkSize = 8)
        {

            serialPort.Open();
            if (!downloadModeSet) { serialPort.DataReceived += SerialDataReceivedHandler_Download; downloadModeSet = true; }
            if (uploadModeSet) { serialPort.DataReceived -= SerialDataReceivedHandler_Upload; uploadModeSet = false; }
            ReceiveBuffer = new Dictionary<UInt32, List<byte>>();
            // Prepare Queue
            UInt32 address = startingAddress;
            for (int i = 0; i < length; i += chunkSize)
            {
                byte len = (byte)Math.Min(chunkSize, length - i);
                readQueue.Enqueue((address, len));
                address += len;

            }

            // Start the first write
            serialPort.DiscardInBuffer();
            ReceiveNextChunk();

            // block until download finished
            autoReset.WaitOne();

            serialPort.Close();
            return ReceiveBuffer;
        }
        public class ResponseClass
        {
            public string status { get; set; }
            public UInt32 address { get; set; }
            public IList<byte> data { get; set; }
        }
        void SerialDataReceivedHandler_Download(object sender, SerialDataReceivedEventArgs e)
        {
            incoming += serialPort.ReadExisting();
            //serialBuffer.Append(incoming);
            if (incoming.Contains("}"))
            {
                JsonDocument jsonResponse = JsonDocument.Parse(incoming.Trim());

                ResponseClass responseJson = jsonResponse.Deserialize<ResponseClass>();

                string responseStatus = jsonResponse.RootElement.GetProperty("status").ToString();
                //Console.WriteLine("response status " + responseStatus);
                if (responseStatus.Contains("err"))
                {
                    string errorMsg = jsonResponse.RootElement.GetProperty("msg").ToString();
                    if (ShowTransferLogs) Console.WriteLine($"\nerror : {errorMsg}");
                    if (ShowTransferLogs) WriteColored($"retrying adddress {responseJson.address}...", ConsoleColor.DarkYellow, writeLine: true);
                    incoming = "";
                    ReceiveNextChunk(false);

                }
                else
                {

                    if (!ReceiveBuffer.ContainsKey(responseJson.address)) ReceiveBuffer.Add(responseJson.address, responseJson.data.ToList());

                    incoming = "";
                    ReceiveNextChunk(true);
                }
            }
        }
        void SerialDataReceivedHandler_Upload(object sender, SerialDataReceivedEventArgs e)
        {

            incoming += serialPort.ReadExisting();
            //serialBuffer.Append(incoming);
            if (incoming.Contains("}"))
            {
                JsonDocument jsonResponse = JsonDocument.Parse(incoming.Trim());

                string responseStatus = jsonResponse.RootElement.GetProperty("status").ToString();
                //Console.WriteLine("response status " + responseStatus);
                if (responseStatus.Contains("err"))
                {
                    string errorMsg = jsonResponse.RootElement.GetProperty("msg").ToString();
                    if (ShowTransferLogs) Console.WriteLine($"error : {errorMsg}");
                    if (ShowTransferLogs) WriteColored("retrying...", ConsoleColor.DarkYellow);
                    incoming = "";
                    SendNextChunk(false);

                }
                else
                {
                    incoming = "";
                    SendNextChunk(true);
                }
            }

        }
        void ReceiveNextChunk(bool prevAck = false)
        {
            if (readQueue.Count == 0)
            {
                if (ShowTransferLogs) WriteColored("\nQueue: download complete.", ConsoleColor.Green, writeLine: true);
                autoReset.Set();
                return;
            }
            if (!prevAck)
            {
                var (address, length) = readQueue.Peek();
                var json = JsonSerializer.Serialize(new
                {
                    cmd = "read",
                    address = address,
                    length = length
                });
                serialPort.Write(json + '\n');
                Thread.Sleep(1);
                if (ShowTransferLogs) WriteColored($"\n Peek     Sent: {address}", ConsoleColor.DarkRed);
            }
            else
            {
                var (address, length) = readQueue.Dequeue();
                var json = JsonSerializer.Serialize(new
                {
                    cmd = "read",
                    address = address,
                    length = length
                });
                serialPort.Write(json + '\n');
                Thread.Sleep(1);
                if (ShowTransferLogs) WriteColored($"\n Dequeue Sent: {address}", ConsoleColor.DarkGreen);
            }
        }
        void SendNextChunk(bool prevAck = false)
        {
            if (writeQueue.Count == 0)
            {
                if (ShowTransferLogs) WriteColored("\n Queue: Upload complete.", ConsoleColor.Green, writeLine: true);
                autoReset.Set();
                return;
            }
            if (!prevAck)
            {
                var (address, data) = writeQueue.Peek();
                var json = JsonSerializer.Serialize(new
                {
                    cmd = "write",
                    address = address,
                    length = data.Count,
                    data = data
                });
                serialPort.Write(json + '\n');
                Thread.Sleep(1);
                if (ShowTransferLogs) WriteColored($"\n Peek     Sent: {address}", ConsoleColor.DarkRed);
            }
            else
            {
                var (address, data) = writeQueue.Dequeue();
                var json = JsonSerializer.Serialize(new
                {
                    cmd = "write",
                    address = address,
                    length = data.Count,
                    data = data
                });
                serialPort.Write(json + '\n');
                Thread.Sleep(1);
                if (ShowTransferLogs) WriteColored($" Dequeue Sent: {address}", ConsoleColor.DarkGreen, writeLine: true);
            }
        }

        byte[] FormatFileName(string fileName)
        {
            byte[] nameBytes = Encoding.ASCII.GetBytes(Path.GetFileNameWithoutExtension(fileName));
            byte[] result = new byte[16];
            int extIndex = fileName.LastIndexOf('.');
            string extension = extIndex >= 0 ? fileName.Substring(extIndex) : "";

            byte[] extBytes = Encoding.ASCII.GetBytes(extension);
            int maxNameLen = 16 - extBytes.Length;

            Array.Copy(nameBytes, 0, result, 0, Math.Min(maxNameLen, nameBytes.Length));
            Array.Copy(extBytes, 0, result, Math.Min(maxNameLen, nameBytes.Length), extBytes.Length);
            return result;
        }

    }
}
