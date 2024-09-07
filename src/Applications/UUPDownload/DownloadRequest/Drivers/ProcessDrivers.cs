﻿/*
 * Copyright (c) Gustave Monce and Contributors
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnifiedUpdatePlatform.Services.Composition.Database;
using UnifiedUpdatePlatform.Services.WindowsUpdate;
using UnifiedUpdatePlatform.Services.WindowsUpdate.Targeting;
using UUPDownload.Options;
using UUPDownload.Downloading;

namespace UUPDownload.DownloadRequest.Drivers
{
    public partial class ProcessDrivers
    {
        internal static void ParseDownloadOptions(BSPDownloadRequestOptions opts)
        {
            CheckAndDownloadUpdates(
                opts.ReportingSku,
                opts.ReportingVersion,
                opts.MachineType,
                opts.FlightRing,
                opts.FlightingBranchName,
                opts.BranchReadinessLevel,
                opts.CurrentBranch,
                opts.ReleaseType,
                opts.SyncCurrentVersionOnly,
                opts.ContentType,
                opts.Mail,
                opts.Password,
                opts.OutputFolder,
                opts.BSPProductVersion,
                opts.Manufacturer,
                opts.Family,
                opts.ProductName,
                opts.SKUNumber,
                opts.BIOSVendor,
                opts.BaseboardManufacturer,
                opts.BaseboardProduct,
                opts.EnclosureType,
                opts.BIOSVersion,
                opts.BIOSMajorRelease,
                opts.BIOSMinorRelease).Wait();
        }

        private static async Task CheckAndDownloadUpdates(OSSkuId ReportingSku,
                    string ReportingVersion,
                    MachineType MachineType,
                    string FlightRing,
                    string FlightingBranchName,
                    string BranchReadinessLevel,
                    string CurrentBranch,
                    string ReleaseType,
                    bool SyncCurrentVersionOnly,
                    string ContentType,
                    string Mail,
                    string Password,
                    string OutputFolder,
                    string BSPProductVersion,
                    string Manufacturer,
                    string Family,
                    string ProductName,
                    string SKUNumber,
                    string BIOSVendor,
                    string BaseboardManufacturer,
                    string BaseboardProduct,
                    string EnclosureType,
                    string BIOSVersion,
                    string BIOSMajorRelease,
                    string BIOSMinorRelease)
        {
            Logging.Log("Checking for updates...");

            CTAC NewestDriverProductCTAC = new(ReportingSku, ReportingVersion, MachineType, FlightRing, FlightingBranchName, BranchReadinessLevel, CurrentBranch, ReleaseType, SyncCurrentVersionOnly, ContentType: ContentType, IsDriverCheck: true);
            string token = string.Empty;
            if (!string.IsNullOrEmpty(Mail) && !string.IsNullOrEmpty(Password))
            {
                token = await MBIHelper.GenerateMicrosoftAccountTokenAsync(Mail, Password);
            }

            string[] ProductGUIDs = ComputerHardwareID.GenerateHardwareIds(Manufacturer, Family, ProductName, SKUNumber, BIOSVendor, BaseboardManufacturer, BaseboardProduct, EnclosureType, BIOSVersion, BIOSMajorRelease, BIOSMinorRelease);

            foreach (string ProductGUID in ProductGUIDs)
            {
                NewestDriverProductCTAC.Products += $"PN={ProductGUID}_{MachineType}&V={BSPProductVersion}&Source=SMBIOS;";
            }

            IEnumerable<UpdateData> NewestDriverProductUpdateData = await FE3Handler.GetUpdates(null, NewestDriverProductCTAC, token, FileExchangeV3UpdateFilter.ProductRelease);

            string outputFolder = OutputFolder.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

            if (!NewestDriverProductUpdateData.Any())
            {
                Logging.Log("No updates found that matched the specified criteria.", Logging.LoggingLevel.Error);
                return;
            }

            for (int i = 0; i < NewestDriverProductUpdateData.Count(); i++)
            {
                UpdateData update = NewestDriverProductUpdateData.ElementAt(i);

                if (update.Xml.LocalizedProperties.Title.Contains("Windows"))
                {
                    continue;
                }

                Logging.Log($"{i}: Title: {update.Xml.LocalizedProperties.Title}");
                Logging.Log($"{i}: Description: {update.Xml.LocalizedProperties.Description}");

                Logging.Log("Gathering update metadata...");

                HashSet<BaseManifest> compDBs = await update.GetCompDBsAsync();

                Logging.Log("BSP Product Name: " + compDBs.First().UUPProduct);
                Logging.Log("BSP Product Version: " + compDBs.First().UUPProductVersion);

                _ = await UnifiedUpdatePlatform.Services.WindowsUpdate.Downloads.UpdateUtils.ProcessUpdateAsync(update, outputFolder, MachineType, new ReportProgress());
            }

            if (Debugger.IsAttached)
            {
                _ = Console.ReadLine();
            }

            Logging.Log("Completed.");
        }
    }
}
