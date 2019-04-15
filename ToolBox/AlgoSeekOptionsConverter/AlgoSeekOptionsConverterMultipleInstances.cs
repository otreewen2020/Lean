/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ikvm.extensions;
using ICSharpCode.SharpZipLib.BZip2;
using QuantConnect.Configuration;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.ToolBox.AlgoSeekOptionsConverter
{
    using Processors = Dictionary<string, List<AlgoSeekOptionsProcessor>>;

    /// <summary>
    ///     Process a directory of algoseek option files into separate resolutions.
    /// </summary>
    public class AlgoSeekOptionsConverterMultipleInstances
    {
        private readonly Resolution _resolution = Resolution.Minute;
        private readonly bool _testing = Config.GetBool("testing", false);

        private readonly ParallelOptions parallelOptionsWriting = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount / 2 };
        private readonly DirectoryInfo _destination;
        private readonly DateTime _referenceDate;
        private readonly FileInfo _remoteOpraFile;
        private readonly DirectoryInfo _source;
        private ConcurrentDictionary<string, Symbol> _underlyingCache;


        public AlgoSeekOptionsConverterMultipleInstances(DateTime referenceDate, string sourceDirectory, string destinationDirectory, FileInfo remoteOpraFile)
        {
            _referenceDate = referenceDate;
            _source = new DirectoryInfo(sourceDirectory);
            _destination = new DirectoryInfo(destinationDirectory);
            _remoteOpraFile = remoteOpraFile;
            Log.DebuggingEnabled = _testing;
            _source.Create();
        }

        /// <summary>
        ///     Give the reference date and source directory, convert the algoseek options data into n-resolutions LEAN format.
        /// </summary>
        public void Convert()
        {
            Log.Trace($"AlgoSeekOptionsConverterMultipleInstances.Convert(): Copying {_remoteOpraFile.Name} into {_source.FullName}");
            var localOpraFile = new FileInfo(Path.Combine(_source.FullName, _remoteOpraFile.Name));
            if (!_testing)
            {
                localOpraFile = _remoteOpraFile.CopyTo(Path.Combine(_source.FullName, _remoteOpraFile.Name));
                Log.Trace(
                $"AlgoSeekOptionsConverterMultipleInstances.Convert(): {localOpraFile.Name} OPRA files for {_referenceDate:yyyy-MM-dd} " +
                $"with total size of {localOpraFile.Length / Math.Pow(1024, 2):N1} MB copied locally.");
            }


            var decompressedOpraFile = new FileInfo(Path.Combine(_source.FullName, Path.GetFileNameWithoutExtension(localOpraFile.Name)));
            Log.Trace($"AlgoSeekOptionsConverterMultipleInstances.Convert(): Decompress {localOpraFile.Name} into {decompressedOpraFile.FullName}");
            var timer = new Stopwatch();
            timer.Start();
            if (!_testing)
            {
                if (!DecompressOpraFile(localOpraFile, decompressedOpraFile))
                {
                    Log.Error($"AlgoSeekOptionsConverterMultipleInstances.Convert(): Decompressing {localOpraFile.Name} failed!");
                    return;
                }
            }

            Log.Trace($"AlgoSeekOptionsConverterMultipleInstances.Convert(): {localOpraFile.Name} decompressed in {timer.Elapsed:g} full size {decompressedOpraFile.Length / Math.Pow(1024, 3):N1} GB.");
            localOpraFile.Delete();

            var thread = new Thread(ProcessOpraFile);
            thread.Priority = ThreadPriority.Highest;
            thread.Start(decompressedOpraFile);
            thread.Join();
        }

        public void ProcessOpraFile(object opraFileInfo)
        {
            var rawDataFile = (FileInfo)opraFileInfo;
            Log.Trace($"AlgoSeekOptionsConverterMultipleInstances.ProcessOpraFile(): Starting processing {rawDataFile.Name}...");
            var processors = new Processors();
            var totalLinesProcessed = 0L;
            var start = DateTime.Now;
            using (var reader = new AlgoSeekOptionsReader(rawDataFile, _referenceDate))
            {
                var previousTime = start;

                // reader contains the data
                if (reader.Current != null)
                {
                    do
                    {
                        var tick = reader.Current;
                        //Add or create the consolidator mechanism for symbol:

                        List<AlgoSeekOptionsProcessor> symbolProcessors;
                        if (!processors.TryGetValue(tick.SecurityRawIdentifier, out symbolProcessors))
                        {
                            symbolProcessors = new List<AlgoSeekOptionsProcessor>(3)
                            {
                                new AlgoSeekOptionsProcessor(tick.SecurityRawIdentifier, _referenceDate, TickType.Trade, _resolution, _destination.FullName),
                                new AlgoSeekOptionsProcessor(tick.SecurityRawIdentifier, _referenceDate, TickType.Quote, _resolution, _destination.FullName),
                                new AlgoSeekOptionsProcessor(tick.SecurityRawIdentifier, _referenceDate, TickType.OpenInterest, _resolution, _destination.FullName)
                            };
                            processors[tick.SecurityRawIdentifier] = symbolProcessors;
                        }

                        // Pass current tick into processor: enum 0 = trade; 1 = quote, , 2 = oi
                        symbolProcessors[(int)tick.Tick.TickType].Process(tick.Tick);

                        if (++totalLinesProcessed % 1000000 == 0)
                        {
                            var now = DateTime.Now;
                            var speed = 1000 / (now - previousTime).TotalSeconds;

                            Log.Trace($"AlgoSeekOptionsConverterMultipleInstances.Convert(): {rawDataFile.Name} - Processed {totalLinesProcessed / 1e6,-3} M lines at {speed:N2} k/sec, " +
                                      $" Memory in use: {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024):N2} MB");
                            previousTime = DateTime.Now;
                        }
                    } while (reader.MoveNext());
                }
            }
            Log.Trace($"AlgoSeekOptionsConverterMultipleInstances.Convert(): Finished processing file {rawDataFile.Name}, {totalLinesProcessed / 1000000L:N2} M lines processed in {DateTime.Now - start:g}" +
                      $"at {totalLinesProcessed / 1000 / (DateTime.Now - start).TotalSeconds:N2} k/sec.");
            if (!_testing) rawDataFile.Delete();

            Log.Trace($"AlgoSeekOptionsConverterMultipleInstances.Convert(): Saving processed ticks to disk...");
            WriteToDisk(processors);
        }

        ///     Write the processor queues to disk
        /// </summary>
        /// <param name="peekTickTime">Time of the next tick in the stream</param>
        /// <param name="step">Period between flushes to disk</param>
        /// <param name="final">Final push to disk</param>
        /// <returns></returns>
        private void WriteToDisk(Processors processors)
        {
            Flush(processors, DateTime.MaxValue, true);

            _underlyingCache = new ConcurrentDictionary<string, Symbol>();
            var filesCounter = 0;
            var start = DateTime.Now;
            Parallel.ForEach(processors, parallelOptionsWriting, securityTicks =>
            //foreach (var securityTicks in processors)
            {
                var symbol = GenerateSymbolFromSecurityRawIdentifier(securityTicks.Key);
                var zipPath = securityTicks.Value.First().GetZipPath(symbol);
                Directory.CreateDirectory(zipPath.FullName);
                var ticksByTickType = securityTicks.Value.ToLookup(t => t.TickType, p => p);
                foreach (var entryTicks in ticksByTickType)
                {
                    var content = FileBuilder(entryTicks.First());
                    if (content != string.Empty)
                    {
                        File.WriteAllText(Path.Combine(zipPath.FullName, entryTicks.First().GetEntryPath(symbol).Name), content);
                        var filesWritten = Interlocked.Increment(ref filesCounter);
                        if (filesWritten % 10000 == 0)
                        {
                            Log.Trace($"AlgoSeekOptionsConverterMultipleInstances.Convert(): {_remoteOpraFile.Name} - {filesWritten} files written " +
                                      $"at {filesWritten / (DateTime.Now - start).TotalSeconds:N2} files/second.");
                        }
                    }
                }
            });
            var totalTime = DateTime.Now - start;
            Log.Trace($"AlgoSeekOptionsConverterMultipleInstances.Convert(): Finished writing {_remoteOpraFile.Name}. {filesCounter} files written in {totalTime:g} " +
                      $"at {filesCounter / totalTime.TotalSeconds:N2} files/second.");

        }

        /// <summary>
        /// Output a list of basedata objects into a string csv line.
        /// </summary>
        /// <param name="processor"></param>
        /// <returns></returns>
        private string FileBuilder(AlgoSeekOptionsProcessor processor)
        {
            var sb = new StringBuilder();
            foreach (var data in processor.Queue)
            {
                sb.AppendLine(LeanData.GenerateLine(data, SecurityType.Option, processor.Resolution));
            }
            return sb.ToString();
        }

        private Symbol GenerateSymbolFromSecurityRawIdentifier(string securityRawIdentifier)
        {
            var contractParts = securityRawIdentifier.split("-");
            var underlying = contractParts[0];
            var optionRight = contractParts[1] == "P" ? OptionRight.Put : OptionRight.Call;
            var expiry = DateTime.MinValue;
            if (!DateTime.TryParseExact(contractParts[2], "yyyyMMdd", null, DateTimeStyles.None, out expiry))
            {
                // sometimes we see the corrupted data with yyyyMMdd, where dd is equal to zeros
                DateTime.TryParseExact(contractParts[2], "yyyyMM", null, DateTimeStyles.None, out expiry);
            }
            var strike = contractParts[3].ToDecimal() / 10000m;

            Symbol underlyingSymbol;

            if (!_underlyingCache.TryGetValue(underlying, out underlyingSymbol))
            {
                underlyingSymbol = Symbol.Create(underlying, SecurityType.Equity, Market.USA);
                _underlyingCache.AddOrUpdate(underlying, underlyingSymbol);
            }

            return Symbol.CreateOption(underlyingSymbol, Market.USA, OptionStyle.American, optionRight, strike, expiry);
        }

        private void Flush(Processors processors, DateTime time, bool final)
        {
            foreach (var symbol in processors.Keys) processors[symbol].ForEach(x => x.FlushBuffer(time, final));
        }

        /// <summary>
        ///     Decompress huge AlgoSeek's opra bz2 files and returns the outcome status.
        /// </summary>
        /// <param name="compressedRawDatafile">Fileinfo of the compressed orpa file.</param>
        /// <param name="rawDatafile">Fileinfo of the uncompressed output file.</param>
        /// <returns>Boolean indicating if this the process was successful.</returns>
        /// <remarks>
        ///     Public static members of the SharpZipLib.BZip2 type are safe for multithreaded operations.
        ///     Source: https://documentation.help/SharpZip/ICSharpCode.SharpZipLib.BZip2.BZip2.html
        /// </remarks>
        /// >
        private static bool DecompressOpraFile(FileInfo compressedRawDatafile, FileInfo rawDatafile)
        {
            var outcome = false;
            using (var fileToDecompressAsStream = compressedRawDatafile.OpenRead())
            using (var decompressedStream = File.Create(rawDatafile.FullName))
            {
                try
                {
                    BZip2.Decompress(fileToDecompressAsStream, decompressedStream);
                    outcome = true;
                }
                catch (Exception ex)
                {
                    Log.Error($"AlgoSeekOptionsConverter.DecompressOpraFile({compressedRawDatafile.Name}, {rawDatafile.Name}): SharpzipLib.BZip2.Decompress returned error: " + ex);
                }
            }

            return outcome;
        }

        /// <summary>
        /// Compress the queue buffers directly to a zip file. Lightening fast as streaming ram-> compressed zip.
        /// </summary>
        public static bool Package(DateTime date, string destinationDirectory)
        {
            var success = true;
            var parallelOptionsZipping = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 5 };

            Log.Trace("AlgoSeekOptionsConverter.Package(): Zipping all files ...");

            var destination = Path.Combine(destinationDirectory, "option");
            var dateMask = date.ToString(DateFormat.EightCharacter);

            var files =
                Directory.EnumerateFiles(destination, dateMask + "*.csv", SearchOption.AllDirectories)
                .GroupBy(x => Directory.GetParent(x).FullName);

            //Zip each file massively in parallel.
            var start = DateTime.Now;
            var zipFilesCounter = 0L;
            Parallel.ForEach(files, file =>
            {
                try
                {
                    var outputFileName = file.Key + ".zip";
                    // Create and open a new ZIP file
                    var filesToCompress = Directory.GetFiles(file.Key, "*.csv", SearchOption.AllDirectories);
                    using (var zip = ZipFile.Open(outputFileName, ZipArchiveMode.Create))
                    {
                        foreach (var fileToCompress in filesToCompress)
                        {
                            // Add the entry for each file
                            zip.CreateEntryFromFile(fileToCompress, Path.GetFileName(fileToCompress), CompressionLevel.Optimal);
                            var filesZipped = Interlocked.Increment(ref zipFilesCounter);
                            if (filesZipped % 10000 == 0)
                            {
                                Log.Trace($"AlgoSeekOptionsConverterMultipleInstances.Package(): {filesZipped} files written " +
                                          $"at {filesZipped / (DateTime.Now - start).TotalSeconds:N2} files/second.");
                            }
                        }
                    }

                    try
                    {
                        Directory.Delete(file.Key, true);
                    }
                    catch (Exception err)
                    {
                        Log.Error("AlgoSeekOptionsConverter.Package(): Directory.Delete returned error: " + err.Message);
                    }
                }
                catch (Exception err)
                {
                    Log.Error("File: {0} Err: {1} Source {2} Stack {3}", file, err.Message, err.Source, err.StackTrace);
                    success = false;
                }
            });

            var totalTime = DateTime.Now - start;
            Log.Trace($"AlgoSeekOptionsConverterMultipleInstances.Convert(): Finished packaging. {zipFilesCounter} files zipped in {totalTime:g} " +
                      $"at {zipFilesCounter / totalTime.TotalSeconds:N2} files/second.");


            return success;
        }


        /// <summary>
        ///     Cleans zip archives and source data folders before run
        /// </summary>
        public void Clean(DateTime date)
        {
            Log.Trace(
                "AlgoSeekOptionsConverterMultipleInstances.Clean(): cleaning all zip and csv files for {0} before start...",
                date.ToShortDateString()
            );
            var extensions = new HashSet<string> { ".zip", ".csv" };
            var destination = Path.Combine(_destination.FullName, "option");
            Directory.CreateDirectory(destination);
            var dateMask = date.ToString(DateFormat.EightCharacter);
            var files = Directory.EnumerateFiles(destination, dateMask + "_" + "*.*", SearchOption.AllDirectories)
                .Where(x => extensions.Contains(Path.GetExtension(x))).ToList();
            Log.Trace("AlgoSeekOptionsConverterMultipleInstances.Clean(): found {0} files..", files.Count);

            //Clean each file massively in parallel.
            Parallel.ForEach(
                files,
                parallelOptionsWriting,
                file =>
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception err)
                    {
                        Log.Error("AlgoSeekOptionsConverterMultipleInstances.Clean(): File.Delete returned error: " + err.Message);
                    }
                }
            );
        }
    }
}