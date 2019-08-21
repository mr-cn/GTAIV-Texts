using System;
using System.IO;
using System.Text;

namespace GxtParser
{
    class Program
    {
        private static string filePath = @"D:\Workspace\C#\GxtParser\american.gxt";

        private static Table[] tables;

        private static int count;

        static void Main(string[] args)
        {
            Console.WriteLine("GTASA/IV GXT Parser");
            Console.WriteLine("Email: admin@mr-cn.net");
            Console.WriteLine();
            if (!new FileInfo(filePath).Exists)
            {
                Console.WriteLine("american.gxt not found!");
                Environment.Exit(1);
            }

            if (!Directory.Exists(new FileInfo(filePath).DirectoryName + $@"\texts"))
            {
                Directory.CreateDirectory(new FileInfo(filePath).DirectoryName + $@"\texts");
            }

            Console.WriteLine($"Set to {filePath}");
            using (FileStream gxtStream = new FileStream(filePath, FileMode.Open))
            {
                // Header
                byte[] HeaderBytes = new byte[4];
                gxtStream.Read(HeaderBytes);
                log("GXT Version", BitConverter.ToInt16(HeaderBytes));
                string encoding = BitConverter.ToInt16(HeaderBytes, 2) == 16 ? "UTF-16" : "ASCII";
                log("Encoding", encoding);

                // Table Block
                byte[] TABLBytes = new byte[8];
                gxtStream.Read(TABLBytes);
                count = BitConverter.ToInt32(TABLBytes, 4) / 12; // start from index 4 to skip const TABL
                tables = new Table[count];
                log("Table Amount", count);
                for (int i = 0; i < count; i++)
                {
                    byte[] TableBytes = new byte[12];
                    gxtStream.Read(TableBytes);
                    string tblName = Encoding.ASCII.GetString(TableBytes, 0, 8).TrimEnd('\0');
                    int offset = BitConverter.ToInt32(TableBytes, 8);

                    tables[i] = new Table(tblName, offset);
                }

                // Table
                for (int i = 0; i < count; i++)
                {
                    string path = new FileInfo(filePath).DirectoryName + $@"\texts\{tables[i].Name}.txt";

                    gxtStream.Seek(tables[i].Position, SeekOrigin.Begin);
                    byte[] TempBytes = new byte[4];
                    gxtStream.Read(TempBytes);

                    // TKEY
                    if (Encoding.ASCII.GetString(TempBytes) != "TKEY")
                    {
                        // For 'main' subtable, offset above points to TKEY;
                        //   for other subtable, offset points to the position 8 bytes before TKEY
                        //      so we should skip them.

                        gxtStream.Seek(8, SeekOrigin.Current);
                    }

                    // Entries
                    byte[] SizeBytes = new byte[4];
                    gxtStream.Read(SizeBytes);
                    int KeyAmount = BitConverter.ToInt32(SizeBytes) / 8;
                    Entry[] entries = new Entry[KeyAmount];
                    for (int j = 0; j < KeyAmount; j++)
                    {
                        byte[] KeyBytes = new byte[8];
                        gxtStream.Read(KeyBytes);
                        int entryPos = BitConverter.ToInt32(KeyBytes);
                        int CRC32 = BitConverter.ToInt32(KeyBytes, 4); // CRC32, also presents the entry name
                        entries[j] = new Entry(CRC32, entryPos);
                    }

                    // TDAT
                    gxtStream.Seek(4, SeekOrigin.Current); // skip const TDAT
                    SizeBytes = new byte[4];
                    gxtStream.Read(SizeBytes);
                    byte[] DataBytes = new byte[BitConverter.ToInt32(SizeBytes)];
                    gxtStream.Read(DataBytes);
                    using (StreamWriter streamWriter = new StreamWriter(new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write), Encoding.UTF8))
                    {
                        streamWriter.WriteLine($"[{tables[i].Name}]");
                        for (int j = 0; j < KeyAmount; j++)
                        {
                            int startIndex = entries[j].Position;
                            int endIndex = GetEndIndex(DataBytes, entries[j].Position); // find first \0\0
                            string value =
                                Encoding.Unicode.GetString(DataBytes, startIndex,
                                    endIndex - startIndex - 1); // -1 means no \0
                            streamWriter.WriteLine($";0x{entries[j].CRC32.ToString("X8")}={value}");
                            streamWriter.WriteLine($"0x{entries[j].CRC32.ToString("X8")}={value}");
                            streamWriter.WriteLine();
                        }
                    }

                    log("Table", tables[i].Name);
                    log("Keys", KeyAmount);
                    Console.WriteLine();
                }
            }

            Console.ReadKey();
        }

        static void log(string title, object data)
        {
            Console.Write($"[{DateTime.Now.ToLongTimeString()}]{title}: ");
            Console.WriteLine(data);
        }

        static int GetEndIndex(byte[] byteArray, int startIndex)
        {
            while (startIndex + 2 < byteArray.Length)
            {
                if (byteArray[startIndex] == 0 && byteArray[startIndex + 1] == 0 && byteArray[startIndex + 2] == 0)
                {
                    return startIndex + 2;
                }

                startIndex++;
            }

            return byteArray.Length - 1;
        }
    }

    struct Table
    {
        public string Name;
        public int Position;

        public Table(string name, int position)
        {
            Name = name;
            Position = position;
        }
    }

    struct Entry
    {
        public int CRC32;
        public int Position;

        public Entry(int crc, int position)
        {
            CRC32 = crc;
            Position = position;
        }
    }
}