using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Xml.Linq;
using System.Xml.XPath;

namespace VsctSourceGenerator
{
    internal sealed class Symbols
    {
        private readonly XElement _element;
        private ImmutableArray<GuidSymbol> _lazyGuidSymbols;

        public Symbols(XElement element)
        {
            if (element.Name.LocalName != "Symbols")
                throw new ArgumentException("Unexpected XML element name", nameof(element));

            _element = element;
        }

        public ImmutableArray<GuidSymbol> GuidSymbols
        {
            get
            {
                return LazyHelper.EnsureInitialized(
                    ref _lazyGuidSymbols,
                    static element =>
                    {
                        IEnumerable<XElement> guidSymbolElements = element.XPathSelectElements("ct:GuidSymbol", CommandTable.Resolver);
                        ImmutableArray<GuidSymbol>.Builder guidSymbolsBuilder = ImmutableArray.CreateBuilder<GuidSymbol>();
                        foreach (XElement guidSymbolElement in guidSymbolElements)
                        {
                            guidSymbolsBuilder.Add(new GuidSymbol(guidSymbolElement));
                        }

                        return guidSymbolsBuilder.ToImmutable();
                    },
                    _element);
            }
        }
    }
}
