# PDF Inspector

![NET](https://img.shields.io/badge/NET-10.0-green.svg)
![License](https://img.shields.io/badge/License-MIT-blue.svg)
![VS2026](https://img.shields.io/badge/Visual%20Studio-2026-white.svg)
![Version](https://img.shields.io/badge/Version-1.0.2026.0-yellow.svg)]

Das Beispiel zeigt, wie über reguläre Ausdrücke Informationen aus PDF-Dokumenten extrahiert werden können. Es dient als Ausgangspunkt für die Entwicklung eigener PDF-Analyse-Tools.
PDF sind in der Haupsache ASCII Dateien. Die Struktur und Metadaten können mit Textoperationen und regulären Ausdrücken analysiert werden. 
Je nachdem wie das PDF erzeug wurde, können mehr oder weniger Meta-Informationen extrahiert werden. 

# Beispielsource
```csharp
PdfInfo info = PdfInspector.Analyze(@"C:\_Downloads\example_065_PDF-A.pdf");
```

PDF-Dokumente können verschiedene Metadaten enthalten, wie z.B. Titel, Autor, Erstellungsdatum, PDF-Version und ob es sich um ein PDF/A-Dokument handelt. Diese sind in verschiedene Tags hinterlegt.
- pdfaid:
- xmp:
- pdf:

zudem können auch Informationen in Attributen hinterlegt sein. Verschlüsslte PDF-Dokumente erschweren die Analyse, da die Metadaten nicht im Klartext vorliegen.

Kernstück ist die Methode `ReadXmpValue`, die sowohl XML-Elemente als auch Attribute aus dem XMP-Metadatenblock eines PDF-Dokuments ausliest:
```csharp
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
```

Mit der Methode *ExtractXmp* können die **XMP** Metadaten aus dem PDF-Dokument extrahiert werden:

```csharp
string xmp = PdfInspector.ExtractXmp(@"C:\_Downloads\example_065_PDF-A.pdf");
```

PDF haben mittlerweile auch ein gewisses Risiko, da diese auch ausführbaren Code oder auch Attachments enthalten können. Daher ist es wichtig, PDF-Dokumente sorgfältig zu analysieren, bevor damit weiter gearbeitet wird.

Ergbnis der Analyse von den Beispiel PDFs
<img src="PdfInspector.png" style="width:650px;"/>