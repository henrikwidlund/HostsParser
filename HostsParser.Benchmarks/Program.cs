﻿using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace HostsParser.Benchmarks
{
    internal static class Program
    {
        private static void Main()
        {
            BenchmarkRunner.Run(typeof(Program).Assembly, ManualConfig.Create(DefaultConfig.Instance)
                .WithOption(ConfigOptions.JoinSummary, true)
                .WithOption(ConfigOptions.DisableLogFile, true));
        }
    }
}