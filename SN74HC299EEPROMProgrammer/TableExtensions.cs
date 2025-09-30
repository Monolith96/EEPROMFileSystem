using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static SN74HC299EEPROMProgrammer.EEPROMFS;

namespace SN74HC299EEPROMProgrammer
{
    public static class HeaderTableExtensions
    {
        public static byte[] ToByteArray(this FileTableStruct fts)
        {
            return Serialize(fts);
        }
        public static List<byte> ToByteList(this FileTableStruct fts)
        {
            return Serialize(fts).ToList();
        }
        private static unsafe byte[] Serialize(this FileTableStruct fTable)
        {
            byte[] result = new byte[22];
            result[0] = 0xAA;
            result[1] = 0x55;
            result[2] = fTable.MemorySize[0];
            result[3] = fTable.MemorySize[1];
            result[4] = fTable.MemorySize[2];

            for (int i = 0; i < 16; i++)
            {
                result[4 + i] = fTable.MemoryName[i];
            }
            result[20] = fTable.MaxEntries;
            result[21] = fTable.UsedEntries;

            return result;
        }


        public static byte[] ToByteArray(this FileDataStruct fds)
        {
            return fds.ToByteArray();
        }
        public static List<byte> ToByteList(this FileDataStruct fds)
        {
            return fds.SerializeToBytes().ToList();
        }

        private static unsafe byte[] SerializeToBytes(this FileDataStruct fds)
        {
            byte[] result = new byte[sizeof(FileDataStruct)];
            for (int i = 0; i < 16; i++)
            {
                result[i] = fds.FileName[i];
            }
            result[16] = fds.StartAddress[0];
            result[17] = fds.StartAddress[1];
            result[18] = fds.StartAddress[2];

            result[19] = fds.EndAddress[0];
            result[20] = fds.EndAddress[1];
            result[21] = fds.EndAddress[2];

            result[22] = fds.Length[0];
            result[23] = fds.Length[1];
            result[24] = fds.Length[2];

            result[25] = fds.FileIndex;

            return result;
        }

        public static List<byte> ToByteList(this List<FileDataStruct> fds)
        {
            List<byte> result = new List<byte>();
            foreach (var item in fds)
            {
                result.AddRange(item.ToByteList());
            }

            return result;
        }
        public static unsafe UInt32 GetTotalUsableMemoryCapacity(this FileTableStruct fts)
        {
            UInt32 sizeoftables = (UInt32)sizeof(FileTableStruct) + ((UInt32)(sizeof(FileDataStruct) * fts.MaxEntries));
            return (GetMemoryCapacity(fts)) - sizeoftables;
        }
        public static unsafe UInt32 GetMemoryCapacity(this FileTableStruct fts)
        {
            UInt32 capacity = (UInt32)fts.MemorySize[0] << 16;
            capacity |= (UInt32)fts.MemorySize[1] << 8;
            capacity |= (UInt32)fts.MemorySize[2] & 0xFF;
            return capacity;
        }
        public static unsafe string GetMemoryName(this FileTableStruct fts)
        {
            byte[] memoryNamebytes = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                memoryNamebytes[i] = fts.MemoryName[i] == '\0' ? ((byte)' ') : fts.MemoryName[i];
            }
            return Encoding.ASCII.GetString(memoryNamebytes);
        }
        public static unsafe List<byte> GetFreeFileIndexes(this FileTableStruct fts, List<FileDataStruct> fds)
        {
            var freeIndexList = new Dictionary<byte, bool>();
            for (int possibleIndex = 0; possibleIndex < fts.GetMaxFiles(); possibleIndex++)
            {
                freeIndexList.Add((byte)possibleIndex, true);
            }
            foreach (var occupiedIndex in fds.GetFileIndexes())
            {
                freeIndexList.Remove(occupiedIndex);
            }

            return freeIndexList.Keys.ToList();
        }

        public static unsafe List<byte> GetFileIndexes(this List<FileDataStruct> fds)
        {
            var indexList = new List<byte>();
            foreach (var item in fds)
            {
                indexList.Add(item.GetFileIndex());
            }
            return indexList;
        }
        public static unsafe FileDataStruct GetFileByIndex(this List<FileDataStruct> fds, byte fileIndex)
        {

            foreach (var item in fds)
            {
                if (item.GetFileIndex() == fileIndex) return item;
            }
            throw new Exception("not found");
            return new FileDataStruct();
        }
        public static unsafe int GetFileInternalIndex(this List<FileDataStruct> fds, byte actualFileIndex)
        {
            for (int currentInternalIndex = 0; currentInternalIndex < fds.Count; currentInternalIndex++)
            {
                if (fds[currentInternalIndex].GetFileIndex() == actualFileIndex) return currentInternalIndex;
            }

            throw new FileNotFoundException("file with specified index was not found");
        }
        public static unsafe UInt32 GetOccupiedCapacity(this List<FileDataStruct> fds)
        {
            UInt32 totalSize = 0;
            foreach (var item in fds)
            {
                totalSize += item.GetFileLength();
            }
            return totalSize;

        }
        public static unsafe UInt32 GetFreeCapacity(this List<FileDataStruct> fds, FileTableStruct fts)
        {

            return fts.GetTotalUsableMemoryCapacity() - GetOccupiedCapacity(fds);

        }

        public static unsafe UInt32 GetFileLength(this FileDataStruct fds)
        {
            UInt32 length = (UInt32)fds.Length[0] << 16;
            length |= (UInt32)fds.Length[1] << 8;
            length |= (UInt32)fds.Length[2] & 0xFF;
            return length;
        }
        public static unsafe byte GetFileIndex(this FileDataStruct fds)
        {
            return fds.FileIndex;
        }
        public static unsafe byte GetMaxFiles(this FileTableStruct fts)
        {
            return fts.MaxEntries;
        }
        public static unsafe byte GetUsedFileEntries(this FileTableStruct fts)
        {
            return fts.UsedEntries;
        }
        public static unsafe UInt32 GetHeaderSize(this FileTableStruct fts)
        {
            return (UInt32)sizeof(FileTableStruct) + ((UInt32)(sizeof(FileDataStruct) * fts.MaxEntries));
        }
        public static unsafe UInt32 GetFileEndAddress(this FileDataStruct fds)
        {
            UInt32 endAddress = (UInt32)fds.EndAddress[0] << 16;
            endAddress |= (UInt32)fds.EndAddress[1] << 8;
            endAddress |= (UInt32)fds.EndAddress[2] & 0xFF;
            return endAddress;

        }
        public static unsafe UInt32 GetFileStartAddress(this FileDataStruct fds)
        {
            UInt32 startAddress = (UInt32)fds.StartAddress[0] << 16;
            startAddress |= (UInt32)fds.StartAddress[1] << 8;
            startAddress |= (UInt32)fds.StartAddress[2] & 0xFF;
            return startAddress;

        }
        public static unsafe UInt32 GetUsableFreeCapacity(this FileTableStruct fts, List<FileDataStruct> lfds)
        {
            if (lfds.Count == 0) return GetTotalUsableMemoryCapacity(fts); //data pool size

            return GetTotalUsableMemoryCapacity(fts) - (GetFileEndAddress(lfds[lfds.Count - 1]) + 1 - GetHeaderSize(fts));

        }
        public static unsafe FileDataStruct AsFileDataStruct(UInt32 startAddress, UInt32 endAddress, UInt32 fileLength, byte fileIndex, string fileName)
        {
            FileDataStruct fdata = new FileDataStruct();
            byte[] fnameBytes = NormalizeFileName(fileName);
            for (int i = 0; i < 16; i++)
            {
                fdata.FileName[i] = fnameBytes[i];
            }
            fdata.StartAddress[0] = (byte)(startAddress >> 16);
            fdata.StartAddress[1] = (byte)(startAddress >> 8);
            fdata.StartAddress[2] = (byte)(startAddress & 0xFF);

            fdata.EndAddress[0] = (byte)(endAddress >> 16);
            fdata.EndAddress[1] = (byte)(endAddress >> 8);
            fdata.EndAddress[2] = (byte)(endAddress & 0xFF);

            fdata.Length[0] = (byte)(fileLength >> 16);
            fdata.Length[1] = (byte)(fileLength >> 8);
            fdata.Length[2] = (byte)(fileLength & 0xFF);

            fdata.FileIndex = fileIndex;

            return fdata;
        }
        public static unsafe FileDataStruct AsFileDataStruct(this byte[] rawData)
        {
            FileDataStruct fdata = new FileDataStruct();

            for (int i = 0; i < 16; i++)
            {
                fdata.FileName[i] = rawData[i];
            }
            fdata.StartAddress[0] = rawData[16];
            fdata.StartAddress[1] = rawData[17];
            fdata.StartAddress[2] = rawData[18];

            fdata.EndAddress[0] = rawData[19];
            fdata.EndAddress[1] = rawData[20];
            fdata.EndAddress[2] = rawData[21];

            fdata.Length[0] = rawData[22];
            fdata.Length[1] = rawData[23];
            fdata.Length[2] = rawData[24];

            fdata.FileIndex = rawData[25];

            return fdata;
        }

        public static unsafe FileTableStruct AsFileTableStruct(UInt32 memSize, string memName, byte maxEntries = 16, byte usedEntries = 0)
        {
            FileTableStruct ftable = new FileTableStruct();

            ftable.FixedBytes[0] = 0xAA; ftable.FixedBytes[1] = 0x55;
            byte[] memNameBytes = NormalizeMemoryName(memName);
            for (int i = 0; i < 16; i++)
            {
                ftable.MemoryName[i] = memNameBytes[i];
            }
            ftable.MemorySize[0] = (byte)(memSize >> 16);
            ftable.MemorySize[1] = (byte)(memSize >> 8);
            ftable.MemorySize[2] = (byte)(memSize);
            ftable.MaxEntries = maxEntries;
            ftable.UsedEntries = usedEntries;
            return ftable;
        }

        public static unsafe FileTableStruct AsFileTableStruct(this byte[] rawData)
        {
            FileTableStruct ftable = new FileTableStruct();

            ftable.FixedBytes[0] = rawData[0]; ftable.FixedBytes[1] = rawData[1];


            ftable.MemorySize[0] = rawData[2];
            ftable.MemorySize[1] = rawData[3];
            ftable.MemorySize[2] = rawData[4];

            for (int i = 0; i < 16; i++)
            {
                ftable.MemoryName[i] = rawData[4 + i];
            }
            ftable.MaxEntries = rawData[21];
            ftable.UsedEntries = rawData[22];

            return ftable;
        }

        public static unsafe string GetFileName(this FileDataStruct fds)
        {

            byte[] fNameBytes = new byte[16];
            for (int i = 0; i < 16; i++)
            {

                fNameBytes[i] = fds.FileName[i];
            }

            string printable = Regex.Replace(Encoding.ASCII.GetString(fNameBytes), @"[\x00-\x1F\x7F]", " ");

            // Replace invalid filesystem characters (Windows): < > : " / \ | ? *
            string fileSafe = Regex.Replace(printable, @"[<>:""/\\|?*]", " ");

            return fileSafe;
        }

        public static List<FileDataStruct> SortByFileLength(this List<FileDataStruct> fds, bool ascending = false)
        {
            List<FileDataStruct> list = new List<FileDataStruct>(fds);
            int n = list.Count;

            for (int i = 0; i < n - 1; i++)
            {
                int targetIndex = i;

                for (int j = i + 1; j < n; j++)
                {
                    if (ascending)
                    {
                        if (list[j].GetFileLength() < list[targetIndex].GetFileLength())
                            targetIndex = j;
                    }
                    else // descending
                    {
                        if (list[j].GetFileLength() > list[targetIndex].GetFileLength())
                            targetIndex = j;
                    }
                }

                // Swap if needed
                if (targetIndex != i)
                {
                    FileDataStruct temp = list[i];
                    list[i] = list[targetIndex];
                    list[targetIndex] = temp;
                }
            }
            return list;
        }

        public static List<FileDataStruct> Compress(this List<FileDataStruct> fds, FileTableStruct fts)
        {
            List<FileDataStruct> newList = new List<FileDataStruct>();

            uint newStartAddress = GetHeaderSize(fts);
            for (int currentFileIndex = 0; currentFileIndex < fds.Count; currentFileIndex++)
            {
                uint length = fds[currentFileIndex].GetFileLength();

                uint newEndAddress = newStartAddress + length - 1;
                newList.Add(AsFileDataStruct(newStartAddress, newEndAddress, length, GetFileIndex(fds[currentFileIndex]), GetFileName(fds[currentFileIndex])));

                // next file starts after current file's last byte
                newStartAddress = newEndAddress + 1;
            }

            return newList;

        }

        public static byte[] NormalizeFileName(string fileName)
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
        public static byte[] NormalizeMemoryName(string memName, bool zeroesAsSpaces = false)
        {
            byte[] nameBytes = Encoding.ASCII.GetBytes(memName);
            byte[] result = new byte[16];

            Array.Copy(nameBytes, 0, result, 0, Math.Min(16, nameBytes.Length));
            if (zeroesAsSpaces)
            {
                for (int currentCharacter = 0; currentCharacter < result.Length; currentCharacter++)
                {
                    if (result[currentCharacter] == '\0') result[currentCharacter] = ((byte)' ');
                }
            }
            return result;
        }

    }
}
