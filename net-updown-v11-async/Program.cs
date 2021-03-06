﻿using CommandLine;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Threading.Tasks;

namespace StoragePerfNet
{
    class Program
    {
        private const string _containerName = "testcontainer";
        private const string _blobName = "testblobupdown";

        public class Options
        {
            [Option("debug")]
            public bool Debug { get; set; }

            [Option('c', "count", Default = 5)]
            public int Count { get; set; }

            [Option('l', "maximumTransferLength")]
            public int? MaximumTransferLength { get; set; }

            [Option('s', "size", Default = 10 * 1024, HelpText = "Size of message (in bytes)")]
            public int Size { get; set; }

            [Option('t', "maximumThreadCount")]
            public int? MaximumThreadCount { get; set; }
        }

        static async Task Main(string[] args)
        {
            if (!GCSettings.IsServerGC)
            {
                throw new InvalidOperationException("Requires server GC");
            }

            var connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");

            await Parser.Default.ParseArguments<Options>(args).MapResult(
                async o => await Run(connectionString, o),
                errors => Task.CompletedTask);
        }

        static async Task Run(string connectionString, Options options)
        {
#if DEBUG
            if (!options.Debug)
            {
                throw new InvalidOperationException("Requires release configuration");
            }
#endif

            CloudStorageAccount.TryParse(connectionString, out var storageAccount);
            var cloudBlobClient = storageAccount.CreateCloudBlobClient();
            var cloudBlobContainer = cloudBlobClient.GetContainerReference(_containerName);
            var cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(_blobName);

            var payload = new byte[options.Size];
            // Initialize payload with stable random data since all-zeros may be compressed or optimized
            (new Random(0)).NextBytes(payload);
            var payloadStream = new MemoryStream(payload, writable: false);

            Console.WriteLine($"Uploading and downloading blob of size {options.Size} with {options.MaximumThreadCount} threads...");
            Console.WriteLine();

            var sw = new Stopwatch();
            for (var i = 0; i < options.Count; i++)
            {
                payloadStream.Seek(0, SeekOrigin.Begin);                

                sw.Restart();
                await cloudBlockBlob.UploadFromStreamAsync(payloadStream);
                sw.Stop();

                var elapsedSeconds = sw.Elapsed.TotalSeconds;
                var megabytesPerSecond = (options.Size / (1024 * 1024)) / elapsedSeconds;
                Console.WriteLine($"Uploaded {options.Size} bytes in {elapsedSeconds:N2} seconds ({megabytesPerSecond:N2} MB/s)");

                sw.Restart();
                await cloudBlockBlob.DownloadToStreamAsync(Stream.Null);
                sw.Stop();

                elapsedSeconds = sw.Elapsed.TotalSeconds;
                megabytesPerSecond = (options.Size / (1024 * 1024)) / elapsedSeconds;
                Console.WriteLine($"Downloaded {options.Size} bytes in {elapsedSeconds:N2} seconds ({megabytesPerSecond:N2} MB/s)");

                Console.WriteLine();
            }
        }
    }
}
