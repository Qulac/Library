using System.Xml;
using System.Xml.Linq;

namespace HomeLibrary.Models;

/// <summary>
/// Модель книги домашней библиотеки.
/// Оглавление хранится в БД в XML-поле вида: <toc><![CDATA[ ...HTML... ]]></toc>
/// </summary>
public class Book
{
    public int BookId { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int? PublishYear { get; set; }
    public string? Publisher { get; set; }
    public string? Isbn { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }

    /// <summary>Оглавление в виде XML-строки (корневой элемент toc с CDATA).</summary>
    public string? TocXml { get; set; }

    /// <summary>HTML-содержимое оглавления (для редактора).</summary>
    public string? TocHtml
    {
        get => TocXml is null ? null : TocXmlHelper.XmlToHtml(TocXml);
        set => TocXml = string.IsNullOrWhiteSpace(value) ? null : TocXmlHelper.HtmlToXml(value);
    }
}

/// <summary>
/// Утилита преобразования HTML-оглавления в XML и обратно.
/// </summary>
public static class TocXmlHelper
{
    private const string RootName = "toc";

    /// <summary>HTML -> XML: оборачивает разметку в корневой элемент с CDATA.</summary>
    public static string HtmlToXml(string html)
    {
        var xml = new XElement(RootName, new XCData(html));
        return xml.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>XML -> HTML: извлекает содержимое CDATA из корневого элемента.</summary>
    public static string XmlToHtml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return string.Empty;
        try
        {
            var element = XElement.Parse(xml);
            if (element.Name.LocalName == RootName)
                return element.Value; // текст CDATA
            return xml;
        }
        catch (XmlException)
        {
            // Не XML — возвращаем как есть
            return xml;
        }
    }
}
