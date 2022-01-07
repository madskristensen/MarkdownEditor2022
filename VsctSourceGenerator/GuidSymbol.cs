using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Xml.Linq;
using System.Xml.XPath;

namespace VsctSourceGenerator
{
    public sealed class GuidSymbol
    {
        private readonly XElement _element;
        private ImmutableArray<IDSymbol> _lazyIDSymbols;

        public GuidSymbol(XElement element)
        {
            if (element.Name.LocalName != "GuidSymbol")
                throw new ArgumentException("Unexpected XML element name", nameof(element));

            _element = element;
        }

        public string Name => _element.Attribute("name")?.Value ?? "";

        public Guid Value
        {
            get
            {
                if (Guid.TryParse(_element.Attribute("value")?.Value, out Guid value))
                    return value;

                return Guid.Empty;
            }
        }

        public ImmutableArray<IDSymbol> IDSymbols
        {
            get
            {
                return LazyHelper.EnsureInitialized(
                    ref _lazyIDSymbols,
                    static element =>
                    {
                        IEnumerable<XElement> idSymbolElements = element.XPathSelectElements("ct:IDSymbol", CommandTable.Resolver);
                        ImmutableArray<IDSymbol>.Builder idSymbolsBuilder = ImmutableArray.CreateBuilder<IDSymbol>();
                        foreach (XElement idSymbolElement in idSymbolElements)
                        {
                            idSymbolsBuilder.Add(new IDSymbol(idSymbolElement));
                        }

                        return idSymbolsBuilder.ToImmutable();
                    },
                    _element);
            }
        }
    }
}