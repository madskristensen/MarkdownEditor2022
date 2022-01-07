using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace VsctSourceGenerator
{
    internal sealed class CommandTable
    {
        internal static IXmlNamespaceResolver Resolver { get; }

        private readonly XElement _element;
        private ImmutableArray<Symbols> _lazySymbols;

        static CommandTable()
        {
            XmlNamespaceManager namespaceManager = new(new NameTable());
            namespaceManager.AddNamespace("ct", "http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable");
            Resolver = namespaceManager;
        }

        public CommandTable(XElement element)
        {
            if (element.Name.LocalName != "CommandTable")
                throw new ArgumentException("Unexpected XML element name", nameof(element));

            _element = element;
        }

        public ImmutableArray<Symbols> Symbols
        {
            get
            {
                return LazyHelper.EnsureInitialized(
                    ref _lazySymbols,
                    static element =>
                    {
                        IEnumerable<XElement> symbolsElements = element.XPathSelectElements("ct:Symbols", Resolver);
                        ImmutableArray<Symbols>.Builder symbolsBuilder = ImmutableArray.CreateBuilder<Symbols>();
                        foreach (XElement symbolsElement in symbolsElements)
                        {
                            symbolsBuilder.Add(new Symbols(symbolsElement));
                        }

                        return symbolsBuilder.ToImmutable();
                    },
                    _element);
            }
        }
    }
}
