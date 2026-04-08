using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MarkdownEditor2022;

// Binding redirects to ensure the newest WebView2 assemblies are loaded
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.Web.WebView2.Core", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "1.0.3485.44", NewVersion = "1.0.3485.44")]
[assembly: ProvideBindingRedirection(AssemblyName = "Microsoft.Web.WebView2.Wpf", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "1.0.3485.44", NewVersion = "1.0.3485.44")]

// Binding redirect for Markdig
[assembly: ProvideBindingRedirection(AssemblyName = "Markdig", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "1.1.0.0", NewVersion = "1.1.0.0")]

// Binding redirects for Markdig transitive dependencies (System.Memory and friends)
[assembly: ProvideBindingRedirection(AssemblyName = "System.Memory", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "4.0.5.0", NewVersion = "4.0.5.0")]
[assembly: ProvideBindingRedirection(AssemblyName = "System.Buffers", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "4.0.5.0", NewVersion = "4.0.5.0")]
[assembly: ProvideBindingRedirection(AssemblyName = "System.Numerics.Vectors", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "4.1.6.0", NewVersion = "4.1.6.0")]
[assembly: ProvideBindingRedirection(AssemblyName = "System.Runtime.CompilerServices.Unsafe", OldVersionLowerBound = "0.0.0.0", OldVersionUpperBound = "6.0.3.0", NewVersion = "6.0.3.0")]

[assembly: AssemblyTitle(Vsix.Name)]
[assembly: AssemblyDescription(Vsix.Description)]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany(Vsix.Author)]
[assembly: AssemblyProduct(Vsix.Name)]
[assembly: AssemblyCopyright(Vsix.Author)]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]
[assembly: InternalsVisibleTo("MarkdownEditor2022.UnitTests")]

[assembly: AssemblyVersion(Vsix.Version)]
[assembly: AssemblyFileVersion(Vsix.Version)]

namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}