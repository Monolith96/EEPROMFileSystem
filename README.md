#  EEPROM Programmer with basic File-System
based on ![SN74HC299](https://github.com/Monolith96/SN74HC299) Arduino library

A lightweight C# library and toolset for working with EEPROMs through a file system-like abstraction.
It includes utilities for **serializing, deserializing, defragmentation, and managing EEPROM file tables**

---
## Screenshots
*Start*:</br>
<img width="1225" height="603" alt="EFS_1" src="https://github.com/user-attachments/assets/4d6609c0-ebf5-4d9e-9351-b9ceff895458" />

*Removed FileIndex 1*:</br>
<img width="1226" height="603" alt="EFS_2" src="https://github.com/user-attachments/assets/2f490779-fe77-4d7b-b0fb-8db61cd91e79" />

*Defragmented*: </br>
<img width="1224" height="603" alt="EFS_3" src="https://github.com/user-attachments/assets/18ae39c0-f289-49d2-a937-eac69f59a03e" />

---


##  Features
-  **FileTable & FileData serialization** → convert to/from `byte[]` and lists for easier coding
-  **Memory calculations** → get free space, used capacity, and header sizes  
-  **File management** → indexing, sorting, compacting, and safe naming  
-  **Utility extensions** → normalization helpers for filenames and memory names  
-  **Debug-friendly** → extension methods keep your code neat and discoverable  

---

To use this program, you need ![EEPROMProgrammer_JSON](https://github.com/Monolith96/SN74HC299/tree/main/examples/EEPROMProgrammer_JSON) sketch of the *SN74HC299* library running on an Arduino.

---
to manipulate files on memomry through this program, you don't need to get to low-level system. just call their respective methods.</br>
here's the way files are stored on the chip (on low level):</br>
*Header Tables* are stored at the beggining of the chip holding memory details and file counts</br>
Like this:</br>
### FileTable
```
     name              addresses       description
byte FixedBytes[2];    // 0-1          0xAA 0x55
byte MemorySize[3];    // 2-3-4        value is in Bytes (e.g. 2048)
byte MemoryName[16];   // 5 to 20      16 bytes max
MaxEntries;            // 21           max entries 
UsedEntries;           // 22           used entries 
```
### FileData (times MaxEntries)
```
     name               addresses
byte FileName[16];     //  0 to 15
byte StartAddress[3];  //  16-17-18
byte EndAddress[3];    //  19-20-21
byte Length[3];        //  22-23-24
byte FileIndex;        //  25
```
after that, files are stored byte-by-byte on specified locations accordingly.
