using System;
using System.Collections.Immutable;

namespace VsctSourceGenerator
{
    internal static class LazyHelper
    {
        public static ImmutableArray<T> EnsureInitialized<T, TArg>(
            ref ImmutableArray<T> array,
            Func<TArg, ImmutableArray<T>> initializer,
            TArg arg)
        {
            if (array is { IsDefault: false } alreadyInitialized)
                return alreadyInitialized;

            ImmutableArray<T> initialValue = initializer(arg);
            ImmutableArray<T> existingValue = ImmutableInterlocked.InterlockedCompareExchange(ref array, initialValue, comparand: default);
            return existingValue.IsDefault ? initialValue : existingValue;
        }
    }
}
