#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Compiler hook required by <c>init</c> accessors and <c>record struct</c>
    /// when targeting .NET Framework 4.8.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}

namespace System
{
    /// <summary>Polyfill for the C# index syntax on .NET Framework 4.8.</summary>
    internal readonly struct Index
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _value = fromEnd ? ~value : value;
        }

        public int GetOffset(int length)
        {
            int offset = _value;
            if (offset < 0)
            {
                offset += length + 1;
            }

            return offset;
        }

        public static implicit operator Index(int value) => new(value);
    }

    /// <summary>Polyfill for the C# range syntax on .NET Framework 4.8.</summary>
    internal readonly struct Range
    {
        public Range(Index start, Index end)
        {
            Start = start;
            End = end;
        }

        public Index Start { get; }

        public Index End { get; }

        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            int start = Start.GetOffset(length);
            int end = End.GetOffset(length);
            if ((uint)end > (uint)length || (uint)start > (uint)end)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return (start, end - start);
        }
    }
}
#endif

namespace WinForms.Markdown
{
    /// <summary>
    /// Compatibility helpers for target frameworks that lack newer runtime APIs.
    /// </summary>
    internal static class Polyfill
    {
        public static int Clamp(int value, int min, int max)
        {
#if NETFRAMEWORK
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
#else
            return Math.Clamp(value, min, max);
#endif
        }
    }
}
