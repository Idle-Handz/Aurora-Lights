using System.Xml;
using Builder.Data;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Helpers;

internal sealed class SyntheticContentScope : IDisposable
{
    public IReadOnlyList<ElementBase> Elements { get; }

    public SyntheticContentScope(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        var parsers = ElementParserFactory.GetParsers().ToList();
        var fallback = new ElementParser();
        Elements = document.DocumentElement!.ChildNodes.OfType<XmlElement>().Select(node =>
        {
            var header = fallback.ParseElementHeader(node);
            return (parsers.FirstOrDefault(parser => parser.ParserType == header.Type) ?? fallback).ParseElement(node);
        }).ToList();
        foreach (var element in Elements) DataManager.Current.ElementsCollection.Add(element);
    }

    public void Dispose()
    {
        foreach (var element in Elements) DataManager.Current.ElementsCollection.Remove(element);
    }
}
