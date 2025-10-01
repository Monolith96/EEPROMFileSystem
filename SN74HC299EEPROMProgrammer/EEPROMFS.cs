using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace SN74HC299EEPROMProgrammer
{

    public class EEPROMFS
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 23)]
        unsafe public struct FileTableStruct
        {                                       // addresses    desc. 
            public fixed byte FixedBytes[2];    // 0-1          0xAA 0x55
            public fixed byte MemorySize[3];    // 2-3-4        value is in bytes
            public fixed byte MemoryName[16];   // 5 to 20         16 bytes max
            public byte MaxEntries;             // 21           max entries < 16
            public byte UsedEntries;            // 22           used entries < 16
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 26)]
        unsafe public struct FileDataStruct
        {
            public fixed byte FileName[16];     //  0 to 15
            public fixed byte StartAddress[3];  //  16-17-18
            public fixed byte EndAddress[3];    //  19-20-21
            public fixed byte Length[3];        //  22-23-24
            public byte FileIndex;              //  25
        }
        public struct DownloadedFile
        {
            public string FileName;
            public byte[] Data;
        }


        private SerialMedium _medium;
        private FileTableStruct fileTable;
        private List<FileDataStruct> files;
        bool _fileTableValid = false;
        public unsafe EEPROMFS(SerialMedium medium)
        {
            _medium = medium as SerialMedium;
            if (_medium == null || _medium.serialPort == null)
            {
                throw new Exception("Serial port was not set.");
            }
        }

        #region HeaderTable Operations
        /// <summary>
        /// Reads the Header Table from memory.
        /// </summary>
        /// <returns></returns>
        public unsafe bool ReadHeader()
        {
            //get fixedbytes
            Console.WriteLine("   EFS: Reading FileTable");
            var header = _medium.DownloadSingle(0, (byte)sizeof(FileTableStruct)).AsFileTableStruct();

            if (header.FixedBytes[0] == 0xAA && header.FixedBytes[1] == 0x55)
            {
                // valid header found.

                _fileTableValid = true;
                files = new List<FileDataStruct>(DownloadFileDataTables());
                return true;
            }
            else
            {
                // invalid header :D
                _fileTableValid = false;
                files = null;
                return false;
            }
        }
        /// <summary>
        /// Creates a new empty header
        /// </summary>
        /// <param name="memSize">Memory size in bytes</param>
        /// <param name="memName">Memory name</param>
        /// <param name="maxEntries">Maximum file entries</param>
        /// <param name="usedEntries">Entries occupied</param>
        public void CreateNewFileTable(UInt32 memSize, string memName, byte maxEntries = 16, byte usedEntries = 0)
        {

            fileTable = HeaderTableExtensions.AsFileTableStruct(memSize, memName, maxEntries, usedEntries);
            Console.WriteLine("   EFS: Creating new FileTable");
            _medium.Upload(0, fileTable.ToByteList());

        }
        public unsafe byte GetFilesCount()
        {
            return (byte)files.Count;
        }
        public unsafe List<FileDataStruct> GetFiles() { return new List<FileDataStruct>(files); }
        /// <summary>
        /// Downloads FileData tables from memory.
        /// </summary>
        /// <returns>FileData tables as List</returns>
        /// <exception cref="Exception"></exception>
        private unsafe List<FileDataStruct> DownloadFileDataTables()
        {
            if (_fileTableValid)
            {
                if (fileTable.UsedEntries > 0)
                {
                    UInt32 address = (UInt32)sizeof(FileTableStruct);
                    byte fdsSize = (byte)sizeof(FileDataStruct);
                    files = new List<FileDataStruct>();
                    for (uint offset = ((uint)sizeof(FileTableStruct)); offset < fdsSize * fileTable.UsedEntries; offset += fdsSize)
                    {
                        files.Add(_medium.DownloadSingle(offset, fdsSize).AsFileDataStruct());
                    }
                    return files;
                }
                else
                {
                    //throw new Exception("No files available.");
                    return new List<FileDataStruct>(); //return an empty list
                }
            }
            else { throw new Exception("FileTable is not validated."); }
        }
        private unsafe void OverWriteFileTable()
        {
            _medium.Upload(0, fileTable.ToByteList());
        }
        private unsafe void OverWriteFileDataTables()
        {
            Console.WriteLine($"   EFS: Updating FileDataTables");
            for (int i = 0; i < files.Count; i++)
            {
                
                _medium.Upload((UInt32)sizeof(FileTableStruct) + ((UInt32)sizeof(FileDataStruct) * (UInt32)i), files[i].ToByteList());
            }
            //files = new List<FileDataStruct>(files);
        }
        public unsafe string GetMemoryName()
        {
            if (!_fileTableValid) throw new Exception("Memory not initialized");
            return fileTable.GetMemoryName();
        }
        /// <summary>
        /// Gets RAW memory capacity excluding headers.
        /// </summary>
        /// <returns></returns>
        public unsafe uint GetCapacity()
        {
            if (!_fileTableValid) return 0;
            return HeaderTableExtensions.GetMemoryCapacity(fileTable);
        }
        /// <summary>
        /// Gets Free Capacity of the memory including gaps.
        /// </summary>
        public unsafe uint GetFreeCapacity()
        {
            if (!_fileTableValid) return 0;

            return HeaderTableExtensions.GetFreeCapacity(files, fileTable);
        }
        /// <summary>
        /// Since new files are added to the end of the files list, remaining space after last file is returned as Usable Free Capacity.
        /// <para>
        /// To remove gaps and get actual free space, run <c>EEPROMFS.Defragment()</c> to compress memory.
        /// </para>
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public unsafe uint GetUsableFreeCapacity()
        {
            if (!_fileTableValid) throw new Exception("fileTable was not initialized");

            return HeaderTableExtensions.GetUsableFreeCapacity(fileTable, files);
        }
        public unsafe bool RemoveFile(byte fileIndex)
        {
            if (!_fileTableValid) throw new Exception("file table was not initialized.");
            if (fileTable.UsedEntries == 0) throw new Exception("index does not exist.");
            if (files.Count == 0) return false;
            FileDataStruct fds = files.GetFileByIndex(fileIndex);
            Console.WriteLine($"   EFS: Removing file at index {fileIndex}\n   fileName {fds.GetFileName()}\n   fileSize {fds.GetFileLength()}\n   fileStartAddress {fds.GetFileStartAddress()}\n   fileEndAddress {fds.GetFileEndAddress()} ");
            files.RemoveAt(files.GetFileInternalIndex(fileIndex));
            fileTable.UsedEntries -= 1;
            
            OverWriteFileTable();
            OverWriteFileDataTables();
            return true;
        }
        #endregion

        #region File Transfer
        public unsafe bool AddDummyFile(uint fileDataLength, string fileName)
        {
            if (!_fileTableValid) throw new Exception("fileTable was not initialized");
            if (fileName == null || fileDataLength == 0) throw new Exception("fileName or fileSize cannot be empty");
            if (fileTable.GetUsedFileEntries() >= fileTable.GetMaxFiles()) throw new Exception("memory full");

            //create new file entry
            UInt32 lastAvailableAddress;

            if (files.Count == 0)
                lastAvailableAddress = (UInt32)(sizeof(FileTableStruct) + (sizeof(FileDataStruct)*fileTable.MaxEntries)) ;
            else
                lastAvailableAddress = files[files.Count - 1].GetFileEndAddress() +1 ;

            

            if ( fileDataLength <= GetUsableFreeCapacity())
            {
                //file fits
                UInt32 endAddress = lastAvailableAddress + ((UInt32)fileDataLength) -1;
                byte newFileIndex = fileTable.GetFreeFileIndexes(files)[0]; //first free index spot
                FileDataStruct newsfds = HeaderTableExtensions.AsFileDataStruct(lastAvailableAddress, endAddress, (UInt32)fileDataLength, newFileIndex ,fileName);
                
                fileTable.UsedEntries++;
                files.Add(newsfds);

                OverWriteFileTable();
                OverWriteFileDataTables();

            }
            else
            {
                throw new Exception("insufficient freespace");
            }

            return false;

        }
        public unsafe bool AddFile(byte[] fileData, string fileName)
        {
            if (!_fileTableValid) throw new Exception("fileTable was not initialized");
            if (fileName == null || fileData.Length == 0) throw new Exception("fileName or fileSize cannot be empty");
            if (fileTable.GetUsedFileEntries() >= fileTable.GetMaxFiles()) throw new Exception("memory full");

            // check if file fits
            if (files.Count == fileTable.GetMaxFiles()) throw new Exception("Memory full.");
            
            //create new file entry
            UInt32 lastAvailableAddress;

            if (files.Count == 0)
                lastAvailableAddress = (UInt32)(sizeof(FileTableStruct) + (sizeof(FileDataStruct) * fileTable.MaxEntries));
            else
                lastAvailableAddress = files[files.Count - 1].GetFileEndAddress() + 1;



            if (fileData.Length <= GetUsableFreeCapacity())
            {
                //file fits
                UInt32 endAddress = lastAvailableAddress + ((UInt32)fileData.Length) - 1;
                byte newFileIndex = fileTable.GetFreeFileIndexes(files)[0]; //first free index spot
                FileDataStruct newsfds = HeaderTableExtensions.AsFileDataStruct(lastAvailableAddress, endAddress, (UInt32)fileData.Length, newFileIndex, fileName);

                //upload file data
                _medium.Upload(lastAvailableAddress, fileData.ToList());

                //add header
                files.Add(newsfds);
                fileTable.UsedEntries++;

                OverWriteFileTable();
                OverWriteFileDataTables();

            }
            else
            {
                throw new Exception("insufficient freespace");
            }

            return false;

        }
        public unsafe DownloadedFile DownloadFile(byte fileIndex)
        {
            FileDataStruct fds = files.GetFileByIndex(fileIndex);
            WriteColored($"\t- processing file{fileIndex} {fds.GetFileName()}...", ConsoleColor.DarkRed, writeLine: true);
            
            var currentFileRawData = _medium.Download(fds.GetFileStartAddress(), (int)fds.GetFileLength(), 16);
            List<byte> data = new List<byte>();
            foreach (var item in currentFileRawData)
            {
                data.AddRange(item.Value);
            }
            DownloadedFile df = new DownloadedFile();
            df.FileName = fds.GetFileName();
            df.Data = data.ToArray();
            return df;
        }
        #endregion


        public unsafe bool Defragment()
        {
            if (!_fileTableValid) throw new Exception("not initialized");
            //download all files
            WriteColored("Downloading files...", ConsoleColor.Red,writeLine:true);
            foreach (var currentFileIndex in files.GetFileIndexes())
            {
                WriteColored($"\t- processing file{currentFileIndex}...", ConsoleColor.DarkRed, writeLine: true);
                FileDataStruct fds = files.GetFileByIndex(currentFileIndex);
                var currentFileRawData = _medium.Download(fds.GetFileStartAddress(), (int)fds.GetFileLength(), 16);
                List<byte> data = new List<byte>();
                foreach (var item in currentFileRawData)
                {
                    data.AddRange(item.Value);
                }
                File.WriteAllBytes($"{fds.GetFileIndex()}", data.ToArray());
            }
            WriteColored("Download complete.", ConsoleColor.Green, writeLine: true);
            WriteColored("Calculating new table...", ConsoleColor.DarkRed, writeLine: true);

            //compact (remove gaps)
            files = files.Compress(fileTable);
            WriteColored("Uploading files...", ConsoleColor.Red, writeLine: true);

            //upload all files
            foreach (var currentFileIndex in files.GetFileIndexes())
            {
                WriteColored($"\t- processing file{currentFileIndex}...", ConsoleColor.DarkRed, writeLine: true);
                FileDataStruct fds = files.GetFileByIndex(currentFileIndex);
                byte[] currentFileRawData = File.ReadAllBytes($"{fds.GetFileIndex()}");
                _medium.Upload(fds.GetFileStartAddress(), currentFileRawData.ToList());
                File.Delete($"{fds.GetFileIndex()}");
            }
            WriteColored("Upload complete.", ConsoleColor.Green, writeLine: true);
            WriteColored("Updating headers...", ConsoleColor.Green, writeLine: true);

            //update headers
            OverWriteFileDataTables();
            return true;
        }

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
    }
}
