//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Lifeprojects.de">
//     Class: Program
//     Copyright © Lifeprojects.de 2026
// </copyright>
// <Template>
// 	Version 3.0.2026.1, 08.1.2026
// </Template>
//
// <author>Gerhard Ahrens - Lifeprojects.de</author>
// <email>developer@lifeprojects.de</email>
// <date>26.01.2026 20:00:39</date>
//
// <summary>
// Konsolen Applikation mit Menü
// </summary>
//-----------------------------------------------------------------------

namespace Console.PdfInspector
{
    /* Imports from NET Framework */
    using System;
    using System.Globalization;
    using System.IO.Enumeration;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;

    public class Program
    {
        private static void Main(string[] args)
        {
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            DemoDataPath = Path.Combine(new DirectoryInfo(currentDirectory).Parent.Parent.Parent.FullName, "DemoDatei");
            if (Directory.Exists(DemoDataPath) == false)
            {
                Directory.CreateDirectory(DemoDataPath);
            }

            ConsoleMenu.Add("1", "PdfInspector Test", () => MenuPoint1());
            ConsoleMenu.Add("X", "Beenden", () => ApplicationExit());

            do
            {
                _ = ConsoleMenu.SelectKey(2, 2);
            }
            while (true);
        }

        internal static string DemoDataPath { get; private set; }


        private static void ApplicationExit()
        {
            Environment.Exit(0);
        }

        private static void MenuPoint1()
        {
            System.Console.Clear();

            var extensions = new List<string> { ".pdf" };
            EnumerationOptions eo = new EnumerationOptions();
            eo.RecurseSubdirectories = true;
            eo.IgnoreInaccessible = true;

            var pdfFiles = new FileSystemEnumerable<(long, string)>(DemoDataPath,
                (ref FileSystemEntry entry) => (entry.Length, entry.ToSpecifiedFullPath()), eo)
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                {
                    // Skip directories.
                    if (entry.IsDirectory == true)
                    {
                        return false;
                    }

                    foreach (string extension in extensions)
                    {
                        var fileExtension = Path.GetExtension(entry.FileName);
                        if (fileExtension.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                        {
                            // Include the file if it matches one of our extensions.
                            return true;
                        }
                    }

                    // Doesn't match, so exclude it.
                    return false;
                }
            };

            /*
            string[] pdfFiles = Directory.GetFiles(DemoDataPath, "*.pdf", eo);
            */

            foreach (var pdfFile in pdfFiles)
            {
                ConsoleMenu.Print($"Prüfe PDF: {ShortenPath(pdfFile.Item2)}");
                PdfInfo info = PdfInspector.Analyze(pdfFile.Item2);
                string xmp = PdfInspector.ExtractXmp(pdfFile.Item2);
                string pdfTyp = PdfInspector.ReadPdfXMP(xmp);

                ConsoleMenu.Print($"{info}");
                ConsoleMenu.Print($"XMP: {pdfTyp}");
                ConsoleMenu.PrintLine();
            }

            ConsoleMenu.Wait();
        }

        internal static string ShortenPath(string fullPath, int maxLength = 50)
        {
            if (string.IsNullOrEmpty(fullPath) || fullPath.Length <= maxLength)
            {
                return fullPath;
            }

            string fileName = Path.GetFileName(fullPath);
            string directory = Path.GetDirectoryName(fullPath) ?? string.Empty;

            // Wenn allein der Dateiname schon zu lang ist → Dateiname kürzen
            if (fileName.Length + 3 >= maxLength)
            {
                return "..." + fileName[^Math.Min(fileName.Length, maxLength - 3)..];
            }

            int remainingLength = maxLength - fileName.Length - 3;
            string shortenedDir = directory.Length > remainingLength
                ? directory[..remainingLength]
                : directory;

            return $"{shortenedDir}...{fileName}";
        }
    }

    public sealed class PdfInfo
    {
        public string PdfVersion { get; init; }
        public string PdfAType { get; init; }
        public bool IsPdfA => PdfAType != null;
        public string CreatorTool { get; init; }
        public string CreateDate { get; init; }
        public string Producer { get; init; }
        public string Keywords { get; init; }

        public override string ToString()
        {
            return $"PDF Version: {PdfVersion}, PDF Type: {PdfAType ?? "n/a"}\nCreatorTool: {this.CreatorTool}\nCreateDate: {this.CreateDate}\nProducer: {this.Producer}\nKeywords: {this.Keywords}";
        }
    }

    public sealed class PdfAttachmentInfo
    {
        public bool HasAttachments { get; init; }
        public int EstimatedCount { get; init; }
    }

    public sealed class PdfAttachment
    {
        public string FileName { get; init; }
        public bool IsUnicode { get; init; }
    }

    public static class PdfInspector
    {
        public static PdfInfo Analyze(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);

            // PDF-Dateien sind ASCII-lastig → Latin1 ist ideal
            string text = Encoding.Latin1.GetString(bytes);

            string pdfVersion = ReadPdfVersion(text);
            (string,string,string,string,string) pdfAType = ReadPdfAType(text);

            return new PdfInfo
            {
                PdfVersion = pdfVersion,
                PdfAType = pdfAType.Item1,
                CreatorTool = pdfAType.Item2,
                CreateDate = pdfAType.Item3,
                Producer = pdfAType.Item4,
                Keywords = pdfAType.Item5
            };
        }

        public static string ExtractXmp(string pdfPath)
        {
            byte[] bytes = File.ReadAllBytes(pdfPath);

            // PDF + XMP sind ASCII-lastig
            string text = Encoding.Latin1.GetString(bytes);

            const string begin = "<?xpacket begin=";
            const string end = "<?xpacket end=";

            int start = text.IndexOf(begin, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            int endPos = text.IndexOf(end, start, StringComparison.OrdinalIgnoreCase);
            if (endPos < 0)
            {
                return null;
            }

            // Ende inkl. '?>'
            int close = text.IndexOf("?>", endPos, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
            {
                return null;
            }

            close += 2;

            return text.Substring(start, close - start);
        }

        public static string ReadPdfXMP(string xmp)
        {
            if (string.IsNullOrWhiteSpace(xmp))
                return null;

            string part = null;
            string conf = null;

            var settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Ignore
            };

            using var reader = XmlReader.Create(new StringReader(xmp), settings);

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                // Element-Syntax
                if (reader.LocalName.ToLower() == "part")
                {
                    part = reader.ReadElementContentAsString();
                }

                if (reader.LocalName.ToLower() == "conformance")
                {
                    conf = reader.ReadElementContentAsString();
                }

                // Attribut-Syntax
                if (reader.HasAttributes)
                {
                    for (int i = 0; i < reader.AttributeCount; i++)
                    {
                        reader.MoveToAttribute(i);

                        if (reader.LocalName.ToLower() == "part")
                        {
                            part = reader.Value;
                        }

                        if (reader.LocalName.ToLower() == "conformance")
                        {
                            conf = reader.Value;
                        }
                    }

                    reader.MoveToElement();
                }
            }

            if (string.IsNullOrWhiteSpace(part) || string.IsNullOrWhiteSpace(conf))
            {
                return null;
            }
            else
            {
                return $"{part}{conf.ToLower()}";
            }
        }

        public static PdfAttachmentInfo DetectAttachment(string pdfPath)
        {
            byte[] bytes = File.ReadAllBytes(pdfPath);
            string text = Encoding.Latin1.GetString(bytes);

            int count = 0;

            // 1️⃣ NameTree für Attachments
            count += CountAttachments(text, "/EmbeddedFiles");

            // 2️⃣ Filespec-Objekte
            count += CountAttachments(text, "/Type /Filespec");

            // 3️⃣ Embedded File Streams
            count += CountAttachments(text, "/EF");

            return new PdfAttachmentInfo
            {
                HasAttachments = count > 0,
                EstimatedCount = count
            };
        }

        public static IReadOnlyList<PdfAttachment> ReadAttachmentNames(string pdfPath)
        {
            byte[] bytes = File.ReadAllBytes(pdfPath);
            string text = Encoding.Latin1.GetString(bytes);

            var results = new List<PdfAttachment>();

            int index = 0;
            while ((index = text.IndexOf("/Type /Filespec", index, StringComparison.Ordinal)) >= 0)
            {
                // Begrenze den Suchbereich auf das Objekt
                int objStart = text.LastIndexOf("<<", index, StringComparison.Ordinal);
                int objEnd = text.IndexOf(">>", index, StringComparison.Ordinal);

                if (objStart < 0 || objEnd < 0 || objEnd <= objStart)
                {
                    index += 10;
                    continue;
                }

                string block = text.Substring(objStart, objEnd - objStart);

                // 1️⃣ Unicode-Name bevorzugen (/UF)
                string uf = ExtractPdfString(block, "/UF");
                if (!string.IsNullOrEmpty(uf))
                {
                    results.Add(new PdfAttachment
                    {
                        FileName = uf,
                        IsUnicode = true
                    });
                }
                else
                {
                    // 2️⃣ Fallback: ASCII (/F)
                    string f = ExtractPdfString(block, "/F");
                    if (!string.IsNullOrEmpty(f))
                    {
                        results.Add(new PdfAttachment
                        {
                            FileName = f,
                            IsUnicode = false
                        });
                    }
                }

                index = objEnd;
            }

            return results;
        }

        private static int CountAttachments(string text, string value)
        {
            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static string ReadPdfVersion(string text)
        {
            var match = Regex.Match(text, @"%PDF-(\d\.\d)");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static (string,string,string,string,string) ReadPdfAType(string text)
        {
            string part = ReadXmpValue(text, "part");
            string conf = ReadXmpValue(text, "conformance");
            string creatorTool = ReadXmpValue(text, "CreatorTool");
            string createDate = ReadXmpValue(text, "CreateDate");
            string producer = ReadXmpValue(text, "Producer");
            string keywords = ReadXmpValue(text, "Keywords");

            if (string.IsNullOrWhiteSpace(part) || string.IsNullOrWhiteSpace(conf))
            {
                return (string.Empty,string.Empty,string.Empty,string.Empty,string.Empty);
            }

            return ($"PDF/A-{part}{conf.ToLower(CultureInfo.CurrentCulture)}", creatorTool, createDate, producer, keywords);
        }

        private static string ReadXmpValue(string text, string tagName)
        {
            /* XML-Element: <pdfaid:part>3</pdfaid:part> */
            var elementMatch = Regex.Match(text, $@"<pdfaid:{tagName}>\s*(.*?)\s*</pdfaid:{tagName}>", RegexOptions.IgnoreCase);

            if (elementMatch.Success == true)
            {
                return elementMatch.Groups[1].Value;
            }
            else
            {
                var elementXmpMatch = Regex.Match(text, $@"<xmp:{tagName}>\s*(.*?)\s*</xmp:{tagName}>", RegexOptions.IgnoreCase);
                if (elementXmpMatch.Success == true)
                {
                    return elementXmpMatch.Groups[1].Value;
                }
                else
                {
                    var elementPdfMatch = Regex.Match(text, $@"<pdf:{tagName}>\s*(.*?)\s*</pdf:{tagName}>", RegexOptions.IgnoreCase);
                    if (elementPdfMatch.Success == true)
                    {
                        return elementPdfMatch.Groups[1].Value;
                    }
                }
            }

            /* Attribut: pdfaid:part="3" */
            var attributeMatch = Regex.Match(text, $@"pdfaid:{tagName}\s*=\s*[""'](.*?)[""']", RegexOptions.IgnoreCase);

            return attributeMatch.Success ? attributeMatch.Groups[1].Value : null;
        }

        private static string ExtractPdfString(string block, string key)
        {
            int keyIndex = block.IndexOf(key + " ", StringComparison.Ordinal);
            if (keyIndex < 0)
                return null;

            int start = block.IndexOf("(", keyIndex, StringComparison.Ordinal);
            int end = block.IndexOf(")", start + 1, StringComparison.Ordinal);

            if (start < 0 || end < 0)
                return null;

            string value = block.Substring(start + 1, end - start - 1);

            // UTF-16BE erkennen (BOM: FE FF)
            if (value.Length >= 2 && value[0] == '\u00FE' && value[1] == '\u00FF')
            {
                byte[] utf16 = Encoding.Latin1.GetBytes(value);
                return Encoding.BigEndianUnicode.GetString(utf16, 2, utf16.Length - 2);
            }

            return value;
        }
    }
}
