using System;

namespace Unity.Collections
{
    static class NativeMultiHashMapExtensions
    {
        public static bool AddValueIfUnique<TKey, TValue>(this NativeMultiHashMap<TKey, TValue> hashMap, TKey key,
            TValue value)
            where TKey : struct, IEquatable<TKey>
            where TValue : struct, IEquatable<TValue>
        {
            if (hashMap.TryGetFirstValue(key, out var existingValue, out var iterator))
            {
                do
                {
                    if (value.Equals(existingValue))
                        return false;
                } while (hashMap.TryGetNextValue(out existingValue, ref iterator));
            }

            hashMap.Add(key, value);
            return true;
        }
    }

    static class NativeListExtensions
    {
        public static bool AddIfUnique<TValue>(this NativeList<TValue> list, TValue value)
            where TValue : struct, IEquatable<TValue>
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (value.Equals(list[i]))
                    return false;
            }

            list.Add(value);
            return true;
        }
    }
}
