#if NETSTANDARD2_0
// Required for C# 9 records (init-only setters) on netstandard2.0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif