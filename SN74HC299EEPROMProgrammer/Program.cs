using System;
using System.IO;
using System.Collections.Generic;

namespace SN74HC299EEPROMProgrammer
{
    class Program
    {
        public static void printInfo(EEPROMFS efs)
        {
            Console.Clear();
            Console.WriteLine("Header data:");
            Console.WriteLine($"   Memory name:                                \t\t {efs.GetMemoryName()}");
            Console.WriteLine($"   total files on memory:                      \t\t {efs.GetFilesCount()}");
            Console.WriteLine($"   total raw memory capacity:                  \t\t {efs.GetCapacity()} Bytes");
            Console.WriteLine($"   total free memory capacity including Gaps:  \t\t {efs.GetFreeCapacity()} Bytes");
            Console.WriteLine($"   total free memory capacity excluding Gaps:  \t\t {efs.GetUsableFreeCapacity() } Bytes\n");
            Console.Write( "   Defragment required: "); Console.WriteLine(efs.GetFreeCapacity() > efs.GetUsableFreeCapacity() ? "YES" : "NO");

            Console.WriteLine("\nlist of files: ");
            List<EEPROMFS.FileDataStruct> lfds = efs.GetFiles();
            foreach (var item in lfds)
            {
                //note that INTERNAL INDEX is the index of the entry in the EFS array, but (external) INDEX is the file index stored on memory.
                Console.WriteLine($"   file entry: internal[{lfds.GetFileInternalIndex(item.GetFileIndex())}] index[{item.GetFileIndex()}]: \t{item.GetFileName()} {item.GetFileLength()} bytes  \trange:[{item.GetFileStartAddress()}]-[{item.GetFileEndAddress()}]");
            }
            //Console.ReadLine();
        }
        static void Main(string[] args)
        {
            /* WARNING! 
                It is best to do your testings and customizations on an SRAM IC before directly
                using an EEPROM and wasting its' writecycles.
                
                Here I have used a "SONY-CXK5863" SRAM which has 8Kx8bit (8192 bytes) capacity.
                Set "eFS" Constructor values according to your own's specs.
                Also check COM port name and baud-rate.
             */
            SerialMedium serialMedium = new SerialMedium();
            serialMedium.serialPort = new System.IO.Ports.SerialPort("COM5", 921600); 

            //-----CLEAR FIRST 100 BYTES OF MEMORY
            byte[] zeroes = new byte[100];
            serialMedium.WriteColored("Clearing headers...", ConsoleColor.Yellow);

            serialMedium.Upload(0, new List<byte>(zeroes));
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.Clear();
            //-------------------------------------
            
            
            EEPROMFS eFS = new EEPROMFS(serialMedium);

            if (!eFS.ReadHeader()) eFS.WriteColored("Invalid Header found. fixing...", ConsoleColor.Red);
            //Console.ReadLine();
            eFS.CreateNewFileTable(8192, "SONY-CXK", 8);
            //Console.ReadLine();
            Console.Clear();
            if (eFS.ReadHeader()) eFS.WriteColored("Header fixed.", ConsoleColor.Green,writeLine: true);
            Console.WriteLine("adding files...");

            // ----- AddDummyFile() ONLY ADDS TABLE DATA TO THE HEADER WITH NO REAL FILE UPLOADS/DATA (FOR TESTING AND DEBUGGING)
            eFS.AddDummyFile(969, "a1.f");
            eFS.AddDummyFile(200, "a2.f");

            // ----- AddFile() UPLOADS REAL FILES FROM COMPUTER
            //eFS.AddFile(File.ReadAllBytes(@"D:\ax.bmp"), "ax.bmp"); 
            //eFS.AddFile(File.ReadAllBytes(@"D:\MTA2.png"), "MTA2.png");

            eFS.AddDummyFile(30, "a3.f");
            //eFS.AddDummyFile(1200, "a4.f");
            //eFS.AddDummyFile(500, "a5.f");




            printInfo(eFS);
            while (true)
            {
                Console.WriteLine("\nUsage: \n");
                Console.Write("   W<fileName>,<fileLength>   to add dummy file\n" +
                                  "   U<filePath>                to upload actual file\n" +
                                  "   D<index>,<pathToSave>      to download a file (do not pass file name)\n" +
                                  "   R<index>                   to remove\n" +
                                  "   C                          clear screen\n" +
                                  "   F                          defragment storage\n" +
                                  "\n"+eFS.GetMemoryName().Trim()+":> ");
                string p = Console.ReadLine().Trim();
                if (p.StartsWith("W"))
                {
                    string fname = p.Substring(1, p.IndexOf(',') - 1);
                    uint flen = Convert.ToUInt32(p.Substring(p.IndexOf(',') + 1));

                    eFS.AddDummyFile(flen, fname);
                }
                else if (p.StartsWith("R"))
                {
                    
                    byte fidx = Convert.ToByte(p.Substring(1));
                    Console.WriteLine($"Remove index {fidx}");
                    eFS.RemoveFile(fidx);
                }
                else if (p.StartsWith("U"))
                {
                    string fpath = p.Substring(1);
                    eFS.AddFile(File.ReadAllBytes(fpath), Path.GetFileName(fpath));
                    Console.WriteLine("File Uploaded.");
                }
                else if (p.StartsWith("D"))
                {
                    byte fidx = Convert.ToByte(p.Substring(1,p.IndexOf(',')-1));
                    string fpath = p.Substring(p.IndexOf(',') + 1);
                    EEPROMFS.DownloadedFile dfs = eFS.DownloadFile(fidx);
                    File.WriteAllBytes(fpath + dfs.FileName, dfs.Data);

                    Console.WriteLine("File Saved.");
                }
                else if (p.StartsWith("C"))
                {
                    Console.Clear();
                }
                else if (p.StartsWith("F"))
                {
                    Console.WriteLine("Defragment in progress...");
                    eFS.Defragment();
                }
                else
                {
                    Console.WriteLine($"{p[0]} is unknown command");
                }
                //eFS.AddFile(new byte[7008], "lastfile.cfg");

                printInfo(eFS);
            }




            Console.WriteLine("end");
            //serialMedium.serialPort.Close();
            Console.ReadLine();
        }
        // EEPROMFS MakeHeader()
    }
}