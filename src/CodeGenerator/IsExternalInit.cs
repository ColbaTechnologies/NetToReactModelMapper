// Required for C# 9 records (init-only setters) on netstandard2.0.
// The compiler resolves `init` accessors by looking for this exact type
// in System.Runtime.CompilerServices — the namespace cannot be changed.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
