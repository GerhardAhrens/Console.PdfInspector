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

            var info = PdfInspector.Analyze(@"C:\_Downloads\example_065_PDF-A.pdf");

            ConsoleMenu.Print($"{info}");

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
    }
}
