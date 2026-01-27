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
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;

    public class Program
    {
        private static void Main(string[] args)
        {
            ConsoleMenu.Add("1", "PdfInspector Test", () => MenuPoint1());
            ConsoleMenu.Add("X", "Beenden", () => ApplicationExit());

            do
            {
                _ = ConsoleMenu.SelectKey(2, 2);
            }
            while (true);
        }

        private static void ApplicationExit()
        {
            Environment.Exit(0);
        }

        private static void MenuPoint1()
        {
            System.Console.Clear();

            PdfInfo info = PdfInspector.Analyze(@"C:\_Downloads\example_065_PDF-A.pdf");
            string xmp = PdfInspector.ExtractXmp(@"C:\_Downloads\example_065_PDF-A.pdf");
            string pdfTyp = PdfInspector.ReadPdfXMP(xmp);

            ConsoleMenu.Print($"{info}");
            ConsoleMenu.Print($"XMP: {pdfTyp}");

            ConsoleMenu.Wait();
        }
    }

    public sealed class PdfInfo
    {
        public string PdfVersion { get; init; }
        public string PdfAType { get; init; }
        public bool IsPdfA => PdfAType != null;

        public override string ToString()
        {
            return $"PDF Version: {PdfVersion}, PDF/A Type: {PdfAType ?? "n/a"}";
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
            string pdfAType = ReadPdfAType(text);

            return new PdfInfo
            {
                PdfVersion = pdfVersion,
                PdfAType = pdfAType
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

            return $"PDF/A-{part}{conf.ToLower()}";
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

        private static string ReadPdfAType(string text)
        {
            string part = ReadXmpValue(text, "part");
            string conf = ReadXmpValue(text, "conformance");

            if (string.IsNullOrWhiteSpace(part) || string.IsNullOrWhiteSpace(conf))
            {
                return null;
            }

            return $"PDF/A-{part}{conf.ToLower(CultureInfo.CurrentCulture)}";
        }

        private static string ReadXmpValue(string text, string tagName)
        {
            /* XML-Element: <pdfaid:part>3</pdfaid:part> */
            var elementMatch = Regex.Match(
                text,
                $@"<pdfaid:{tagName}>\s*(.*?)\s*</pdfaid:{tagName}>",
                RegexOptions.IgnoreCase);

            if (elementMatch.Success == true)
            {
                return elementMatch.Groups[1].Value;
            }

            /* Attribut: pdfaid:part="3" */
            var attributeMatch = Regex.Match(
                text,
                $@"pdfaid:{tagName}\s*=\s*[""'](.*?)[""']",
                RegexOptions.IgnoreCase);

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
