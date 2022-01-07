using System;
using System.Globalization;
using System.Xml.Linq;

namespace VsctSourceGenerator
{
    public sealed class IDSymbol
    {
        private readonly XElement _element;

        public IDSymbol(XElement element)
        {
            if (element.Name.LocalName != "IDSymbol")
                throw new ArgumentException("Unexpected XML element name", nameof(element));

            _element = element;
        }

        public string Name => _element.Attribute("name")?.Value ?? "";

        public int Value
        {
            get
            {
                string value = _element.Attribute("value")?.Value ?? "";
                if (value.StartsWith("0x"))
                {
                    if (int.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int numericValue))
                        return numericValue;

                    return 0;
                }
                else
                {
                    if (int.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out int numericValue))
                        return numericValue;

                    return 0;
                }
            }
        }
    }
}
