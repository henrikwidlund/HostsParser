﻿// Copyright Henrik Widlund
// GNU General Public License v3.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace HostsParser
{
    internal static class CollectionUtilities
    {
        internal static List<string> SortDnsList(IEnumerable<string> dnsList, bool distinct)
        {
            return (distinct ? dnsList.Distinct() : dnsList)
                .OrderBy(l => GetTopMostDns(l).ToString())
                .ThenBy(l => l.Length)
                .ToList();
        }

        internal static void FilterGrouped(List<string> dnsList, ref HashSet<string> filtered)
        {
            var hashSet = new HashSet<string>(dnsList);
        
            var dnsGroups = GroupDnsList(dnsList);
            foreach (var (key, value) in dnsGroups)
            {
                if (!hashSet.Contains(key)
                    || value.Count < 2)
                    continue;
                
                for (var index = 0; index < value.Count; index++)
                {
                    var current = value[index];
                    if (key == current)
                        continue;
                    
                    filtered.Add(current);
                }
            }
        }
        
        internal static Dictionary<string, List<string>> GroupDnsList(List<string> dnsList)
        {
            var dict = new Dictionary<string, List<string>>();
            foreach (var s in dnsList)
            {
                var key = GetTopMostDns(s).ToString();
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

        private static ReadOnlySpan<char> GetTopMostDns(ReadOnlySpan<char> item)
        {
            var indexes = GetIndexes(item);
            return indexes.Count <= 1 ? item : ProcessItem(indexes, item);
        }

        private static List<int> GetIndexes(ReadOnlySpan<char> item)
        {
            var foundIndexes = new List<int>();
            for (var i = item.IndexOf(Constants.DotSign); i > -1; i = item.IndexOf(Constants.DotSign, i +1))
                foundIndexes.Add(i);

            return foundIndexes;
        }
        
        private static ReadOnlySpan<char> ProcessItem(List<int> indexes, ReadOnlySpan<char> l)
        {
            if (indexes.Count != 2)
            {
                ReadOnlySpan<char> dns;
                var secondTop = l[(indexes[^2] + 1)..indexes[^1]];
                if (secondTop.Equals(Constants.TopDomains.Co, StringComparison.Ordinal)
                    || secondTop.Equals(Constants.TopDomains.Com, StringComparison.Ordinal)
                    || secondTop.Equals(Constants.TopDomains.Org, StringComparison.Ordinal))
                    dns = l[(indexes[^3] + 1)..];
                else
                    dns = l[(indexes[^2] + 1)..];
                
                return dns.Length > 3 ? dns : l[(indexes[^3] + 1)..];
            }
            
            var item = l[(indexes[0] + 1)..indexes[1]];
            return item.Length <= 3 ? l : l[(indexes[0] + 1)..];
        }

        private static int IndexOf(this ReadOnlySpan<char> aSpan, char aChar, int startIndex)
        {
            var indexInSlice = aSpan[startIndex..].IndexOf(aChar);

            if (indexInSlice == -1)
                return -1;

            return startIndex + indexInSlice;
        }
    }
}
