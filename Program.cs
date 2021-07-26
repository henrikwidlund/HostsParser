﻿// Copyright Henrik Widlund
// GNU General Public License v3.0

using HostsParser;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using var loggerFactory = LoggerFactory.Create(options =>
{
    options.AddDebug();
    options.AddSimpleConsole(consoleOptions =>
    {
        consoleOptions.SingleLine = true;
    });
});
var logger = loggerFactory.CreateLogger("HostsParser");

logger.LogInformation(WithTimeStamp("Running..."));
var stopWatch = new Stopwatch();
stopWatch.Start();

var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllBytes("appsettings.json"));
if (settings == null)
{
    logger.LogError("Couldn't load settings. Terminating...");
    return;
}

var decoder = Encoding.UTF8.GetDecoder();
using var httpClient = new HttpClient();

logger.LogInformation(WithTimeStamp("Start get source hosts"));
var bytes = await httpClient.GetByteArrayAsync(settings.SourceUri);
var sourceUris = HostUtilities.ProcessSource(bytes, settings.SkipLines, decoder);
logger.LogInformation(WithTimeStamp("Done get source hosts"));

logger.LogInformation(WithTimeStamp("Start get AdGuard hosts"));
bytes = await httpClient.GetByteArrayAsync(settings.AdGuardUri);
var adGuardLines = HostUtilities.ProcessAdGuard(bytes, decoder);
logger.LogInformation(WithTimeStamp("Done get AdGuard hosts"));

logger.LogInformation(WithTimeStamp("Start combining host sources"));
var combined = sourceUris
    .Except(adGuardLines)
    .ToList();
sourceUris = null;

Dictionary<string, string> knownBadHostsDictionary = new(combined.Count);
for (var i = 0; i < settings.KnownBadHosts.Length; i++)
{
    var dnsEntry = new DnsEntry(settings.KnownBadHosts[i]);
    knownBadHostsDictionary.Add(dnsEntry.UnPrefixed, dnsEntry.Prefixed);
}
combined.RemoveAll(l => settings.KnownBadHosts.Any(s =>
    knownBadHostsDictionary.TryGetValue(s, out var badEntry)
    && l.EndsWith(badEntry)));

combined = combined.Concat(settings.KnownBadHosts).ToList();
var (withPrefix, withoutPrefix) = CollectionUtilities.GetWwwOnly(combined);
combined = CollectionUtilities.SortDnsList(combined.Except(withPrefix).Concat(withoutPrefix)
    .Concat(adGuardLines), true);

logger.LogInformation(WithTimeStamp("Done combining host sources"));

logger.LogInformation(WithTimeStamp("Start filtering duplicates - Part 1"));
var superFiltered = new List<string>(combined.Count);

var round = 0;
Dictionary<string, string> cachedEntries = new(combined.Count);
for (var i = 0; i < combined.Count; i++)
{
    var dnsEntry = new DnsEntry(combined[i]);
    cachedEntries.Add(dnsEntry.UnPrefixed, dnsEntry.Prefixed);
}

var dnsGroups = CollectionUtilities.GroupDnsList(combined);
foreach (var dnsGroup in dnsGroups)
{
    if (!cachedEntries.ContainsKey(dnsGroup.Key))
        continue;
    
    foreach (var dnsEntry in dnsGroup)
    {
        if (dnsGroup.Key == dnsEntry)
            continue;
        
        superFiltered.Add(dnsEntry);
    }
}

combined = CollectionUtilities.SortDnsList(combined.Except(superFiltered), false);

do
{
    superFiltered.Clear();
    var lookBack = ++round * 250;
    Parallel.For(0, combined.Count, i =>
    {
        for (var j = (i < lookBack ? 0 : i - lookBack); j < i; j++)
        {
            var item = combined[i];
            var otherItem = combined[j];
            if (otherItem.Length + 1 > item.Length) continue;
            if (item == otherItem) continue;
    
            if (cachedEntries.TryGetValue(otherItem, out var cachedEntry)
                && item.EndsWith(cachedEntry))
                superFiltered.Add(item);
        }
    });

    combined = CollectionUtilities.SortDnsList(round == 1
        ? combined.Except(superFiltered).Except(adGuardLines)
        : combined.Except(superFiltered),
        false);
} while (superFiltered.Any());
logger.LogInformation(WithTimeStamp("Done filtering duplicates - Part 1"));

if (settings.ExtraFiltering)
{
    logger.LogInformation(WithTimeStamp("Start filtering duplicates - Part 2"));
    Parallel.ForEach(CollectionUtilities.SortDnsList(adGuardLines, true), item =>
    {
        for (var i = 0; i < combined.Count; i++)
        {
            var localItem = combined[i];
            var cachedEntry = cachedEntries[item];
            if (localItem.EndsWith(cachedEntry))
                superFiltered.Add(localItem);
        }
    });
    combined = CollectionUtilities.SortDnsList(combined.Except(superFiltered), false);
    logger.LogInformation(WithTimeStamp("Done filtering duplicates - Part 2"));
}

logger.LogInformation(WithTimeStamp("Start formatting hosts"));
var newLinesList = combined
    .Select(l => $"||{l}^");
logger.LogInformation(WithTimeStamp("Done formatting hosts"));

logger.LogInformation(WithTimeStamp("Start building hosts results"));

var newLines = new HashSet<string>(settings.HeaderLines) { $"! Last Modified: {DateTime.UtcNow:u}", string.Empty };
foreach (var item in newLinesList)
    newLines.Add(item);

logger.LogInformation(WithTimeStamp("Done building hosts results"));

logger.LogInformation(WithTimeStamp("Start writing hosts file"));
await File.WriteAllLinesAsync("hosts", newLines);
logger.LogInformation(WithTimeStamp("Done writing hosts file"));

stopWatch.Stop();
logger.LogInformation(WithTimeStamp($"Execution duration - {stopWatch.Elapsed} | Produced {combined.Count} hosts"));

static string WithTimeStamp(string message)
{
    return $"{DateTime.Now:yyyy-MM-dd hh:mm:ss} - {message}";
}