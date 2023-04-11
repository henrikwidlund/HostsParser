// Copyright Henrik Widlund
// GNU General Public License v3.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HostsParser;

public static class CollectionUtilities
{
    /// <summary>
    /// Sorts <paramref name="dnsCollection"/> by domain and length.
    /// </summary>
    /// <param name="dnsCollection">The collection to sort.</param>
    public static List<string> SortDnsList(ICollection<string> dnsCollection)
    {
        List<string> list = new(dnsCollection.Count);
        list.AddRange(dnsCollection
            .Select(d => new StringSortItem(d))
            .OrderBy(l => GetTopMostDns(l.RawMemory), ReadOnlyMemoryCharComparer.Default)
            .ThenBy(l => l.RawMemory.Length)
            .Select(l => l.Raw));

        return list;
    }

    /// <summary>
    /// Filters out all sub domains from <paramref name="dnsCollection"/> for which a domain is contained.
    /// </summary>
    /// <param name="dnsCollection">The collection that will be filtered.</param>
    public static void FilterGrouped(HashSet<string> dnsCollection)
    {
        var cacheHashSet = CreateCacheHashSet(dnsCollection);

        var dnsGroups = GroupDnsList(dnsCollection);
        HashSet<string> filtered = new(dnsCollection.Count);
        foreach (var (key, value) in dnsGroups)
        {
            if (!cacheHashSet.Contains(key)
                || value.Count < 2)
                continue;

            foreach (var item in value)
            {
                if (key == item.GetHashCode())
                    continue;

                filtered.Add(item);
            }
        }

        dnsCollection.ExceptWith(filtered);
    }

    /// <summary>
    /// Groups <paramref name="dnsCollection"/> into a dictionary where the key is the main domain
    /// and value is a list of found sub domains.
    /// </summary>
    /// <param name="dnsCollection">The collection used for grouping.</param>
    public static Dictionary<int, List<string>> GroupDnsList(HashSet<string> dnsCollection)
    {
        var dict = new Dictionary<int, List<string>>(dnsCollection.Count);
        foreach (var s in dnsCollection)
        {
            var key = string.GetHashCode(GetTopMostDns(s));
            List<string> values;
            if (!dict.ContainsKey(key))
            {
                values = new List<string>();
                dict.Add(key, values);
            }
            else
            {
                values = dict[key];
            }

            values.Add(s);
        }

        return dict;
    }

    private static HashSet<int> CreateCacheHashSet(HashSet<string> dnsList)
    {
        var hashSet = new HashSet<int>(dnsList.Count);
        foreach (var s in dnsList) hashSet.Add(s.GetHashCode());

        return hashSet;
    }

    private static ReadOnlySpan<char> GetTopMostDns(in ReadOnlySpan<char> item)
    {
        var indices = GetIndices(item);
        return indices.Count <= 1 ? item : ProcessItem(indices, item);
    }

    private static ReadOnlyMemory<char> GetTopMostDns(in ReadOnlyMemory<char> item)
    {
        var indices = GetIndices(item.Span);
        return indices.Count <= 1 ? item : ProcessItem(indices, item);
    }

    private static List<int> GetIndices(in ReadOnlySpan<char> item)
    {
        var foundIndices = new List<int>();
        for (var i = item.IndexOf(Constants.DotSign); i > -1; i = item.IndexOf(Constants.DotSign, i + 1))
            foundIndices.Add(i);

        return foundIndices;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSecondLevelTopDomain(in ReadOnlySpan<char> secondTop)
    {
        return secondTop.Equals(Constants.TopDomains.Co.Span, StringComparison.Ordinal)
               || secondTop.Equals(Constants.TopDomains.Com.Span, StringComparison.Ordinal)
               || secondTop.Equals(Constants.TopDomains.Org.Span, StringComparison.Ordinal)
               || secondTop.Equals(Constants.TopDomains.Ne.Span, StringComparison.Ordinal)
               || secondTop.Equals(Constants.TopDomains.Net.Span, StringComparison.Ordinal)
               || secondTop.Equals(Constants.TopDomains.Edu.Span, StringComparison.Ordinal)
               || secondTop.Equals(Constants.TopDomains.Or.Span, StringComparison.Ordinal);
    }

    private static ReadOnlySpan<char> ProcessItem(List<int> indices,
        in ReadOnlySpan<char> item)
    {
        if (indices.Count != 2)
        {
            var secondTop = item[(indices[^2] + 1)..indices[^1]];
            var dns = IsSecondLevelTopDomain(secondTop)
                ? item[(indices[^3] + 1)..]
                : item[(indices[^2] + 1)..];

            return dns.Length > 3 ? dns : item[(indices[^3] + 1)..];
        }

        var slicedItem = item[(indices[0] + 1)..indices[1]];
        // Check domains ending with x.y where x is shorter than 4 char against known second level top domains.
        // If false, treat x.y as a domain so that any found sub domain will be sorted under it.
        return IsSecondLevelTopDomain(slicedItem) ? item : item[(indices[0] + 1)..];
    }

    private static ReadOnlyMemory<char> ProcessItem(List<int> indices,
        in ReadOnlyMemory<char> item)
    {
        if (indices.Count != 2)
        {
            var secondTop = item[(indices[^2] + 1)..indices[^1]];
            var dns = IsSecondLevelTopDomain(secondTop.Span)
                ? item[(indices[^3] + 1)..]
                : item[(indices[^2] + 1)..];

            return dns.Length > 3 ? dns : item[(indices[^3] + 1)..];
        }

        var slicedItem = item[(indices[0] + 1)..indices[1]];
        // Check domains ending with x.y where x is shorter than 4 char against known second level top domains.
        // If false, treat x.y as a domain so that any found sub domain will be sorted under it.
        return IsSecondLevelTopDomain(slicedItem.Span) ? item : item[(indices[0] + 1)..];
    }

    private static int IndexOf(in this ReadOnlySpan<char> span,
        in char value,
        in int startIndex)
    {
        var indexInSlice = span[startIndex..].IndexOf(value);

        if (indexInSlice == -1)
            return -1;

        return startIndex + indexInSlice;
    }
}

file readonly struct StringSortItem
{
    public readonly string Raw;
    public readonly ReadOnlyMemory<char> RawMemory;

    public StringSortItem(string raw)
    {
        Raw = raw;
        RawMemory = raw.AsMemory();
    }
}
