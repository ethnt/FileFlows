﻿using System.Runtime.InteropServices;
using FileFlows.Server.Helpers;
using FileFlows.Server.Services;
using FileFlows.ServerShared.Workers;
using FileFlows.Shared.Helpers;
using FileFlows.Shared.Models;
using FlowService = FileFlows.Server.Services.FlowService;
using LibraryFileService = FileFlows.Server.Services.LibraryFileService;
using LibraryService = FileFlows.Server.Services.LibraryService;
using NodeService = FileFlows.Server.Services.NodeService;

namespace FileFlows.Server.Workers;

public class TelemetryReporter : ServerWorker
{
    public TelemetryReporter() : base(ScheduleType.Daily, 5)
    {
        Trigger();
    }

    /// <inheritdoc />
    protected override void ExecuteActual(Settings settings)
    {
        try
        {
// #if (DEBUG && false)
//             return;
// #else
            if (settings.DisableTelemetry == true && LicenseHelper.IsLicensed())
                return; // they have turned it off, dont report anything

            bool isDocker = Application.Docker;

            TelemetryData data = new TelemetryData();
            data.ClientUid = settings.Uid;
            data.Version = Globals.Version;
            data.DatabaseProvider = ServiceLoader.Load<AppSettingsService>().Settings.DatabaseType.ToString();
            var pNodes = ServiceLoader.Load<NodeService>().GetAllAsync().Result.Where(x => x.Enabled).ToList();
            data.ProcessingNodes = pNodes.Count();
            data.ProcessingNodeData = pNodes.Select(x => new ProcessingNodeData()
            {
                OS = x.OperatingSystem,
                Architecture = x.Architecture,
                Runners = x.FlowRunners,
                Internal = x.Uid == CommonVariables.InternalNodeUid || x.Name == CommonVariables.InternalNodeName
            }).ToList();
            data.Architecture = RuntimeInformation.ProcessArchitecture.ToString();
            data.OS = isDocker ? "Docker" :
                OperatingSystem.IsMacOS() ? "MacOS" :
                OperatingSystem.IsLinux() ? "Linux" :
                OperatingSystem.IsFreeBSD() ? "FreeBSD" :
                OperatingSystem.IsWindows() ? "Windows" :
                RuntimeInformation.OSDescription;

            var lfService = ServiceLoader.Load<LibraryFileService>();
            var libFileStatus = lfService.GetStatus().Result;

            int filesFailed = libFileStatus
                .Where(x => x.Status == FileStatus.ProcessingFailed)
                .Select(x =>x.Count)
                .FirstOrDefault();
            int filesProcessed = libFileStatus
                .Where(x => x.Status == FileStatus.Processed)
                .Select(x =>x.Count)
                .FirstOrDefault();
            
            data.FilesFailed = filesFailed;
            data.FilesProcessed = filesProcessed;
            var repo = ServiceLoader.Load<RepositoryService>().GetRepository().Result ?? new ();
            var repoScripts = repo.FlowScripts.Union(repo.SharedScripts).Union(repo.SystemScripts)
                .Select(x => x.Name).Distinct().ToList();
            
            var flows = ServiceLoader.Load<FlowService>().GetAllAsync().Result;
            var dictNodes = new Dictionary<string, int>();
            foreach (var fp in flows?.SelectMany(x => x.Parts)?.ToArray() ?? new FlowPart[] { })
            {
                if (fp == null)
                    continue;
                if (fp.FlowElementUid.StartsWith("SubFlow:"))
                    continue;
                if (fp.FlowElementUid.StartsWith("Script:"))
                {
                    string name = fp.FlowElementUid[7..];
                    if (repoScripts.Contains(name) == false)
                        continue;
                }
                if (!dictNodes.TryAdd(fp.FlowElementUid, 1))
                    dictNodes[fp.FlowElementUid] += 1;
            }

            data.Nodes = dictNodes.Select(x => new TelemetryDataSet
            {
                Name = x.Key,
                Count = x.Value
            }).ToList();

            var libraries = ServiceLoader.Load<LibraryService>().GetAllAsync().Result;
            dictNodes.Clear();
            foreach (var lib in libraries?.Where(x => string.IsNullOrEmpty(x.Template) == false) ?? new List<Library>())
            {
                if (!dictNodes.TryAdd(lib.Template, 1))
                    dictNodes[lib.Template] += 1;
            }

            data.StorageSaved = lfService.GetTotalStorageSaved().Result;

            data.LibraryTemplates = dictNodes.Select(x => new TelemetryDataSet
            {
                Name = x.Key,
                Count = x.Value
            }).ToList();


            dictNodes.Clear();
            foreach (var lib in flows?.Where(x => string.IsNullOrEmpty(x.Template) == false) ?? new List<Flow>())
            {
                if (!dictNodes.TryAdd(lib.Template, 1))
                    dictNodes[lib.Template] += 1;
            }

            data.FlowTemplates = dictNodes.Select(x => new TelemetryDataSet
            {
                Name = x.Key,
                Count = x.Value
            }).ToList();

            string url = Globals.FileFlowsDotComUrl + "/api/telemetry";
            _ = HttpHelper.Post(url, data).Result;

//#endif
        }
        catch (Exception)
        {
            // FF-410: silent fail, may not have an internet connection
        }
    }


    /// <summary>
    /// Represents telemetry data collected from a client.
    /// </summary>
    public class TelemetryData
    {
        /// <summary>
        /// Gets or sets the unique identifier of the client.
        /// </summary>
        public Guid ClientUid { get; set; }

        /// <summary>
        /// Gets or sets the version of the telemetry data.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the operating system of the client.
        /// </summary>
        public string OS { get; set; }

        /// <summary>
        /// Gets or sets the architecture of the client's operating system.
        /// </summary>
        public string Architecture { get; set; }

        /// <summary>
        /// Gets or sets the number of processing nodes in the client.
        /// </summary>
        public int ProcessingNodes { get; set; }

        /// <summary>
        /// Gets or sets the data of individual processing nodes in the client.
        /// </summary>
        public List<ProcessingNodeData> ProcessingNodeData { get; set; }

        /// <summary>
        /// Gets or sets the telemetry data sets collected from various nodes in the client.
        /// </summary>
        public List<TelemetryDataSet> Nodes { get; set; }

        /// <summary>
        /// Gets or sets the telemetry data sets for library templates in the client.
        /// </summary>
        public List<TelemetryDataSet> LibraryTemplates { get; set; }

        /// <summary>
        /// Gets or sets the telemetry data sets for flow templates in the client.
        /// </summary>
        public List<TelemetryDataSet> FlowTemplates { get; set; }

        /// <summary>
        /// Gets or sets the number of files processed by the client.
        /// </summary>
        public int FilesProcessed { get; set; }

        /// <summary>
        /// Gets or sets the number of files that failed during processing by the client.
        /// </summary>
        public int FilesFailed { get; set; }
        
        /// <summary>
        /// Gets or sets the amount of storage saved
        /// </summary>
        public long StorageSaved { get; set; }
        
        /// <summary>
        /// Gets or sets the db provider they are using
        /// </summary>
        public string DatabaseProvider { get; set; }
    }

    /// <summary>
    /// Represents a telemetry data set with a name and a count value.
    /// </summary>
    public class TelemetryDataSet
    {
        /// <summary>
        /// Gets or sets the name of the telemetry data set.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the count value of the telemetry data set.
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Represents the data related to a processing node, including its operating system and architecture.
    /// </summary>
    public class ProcessingNodeData
    {
        /// <summary>
        /// Gets or sets the operating system of the processing node.
        /// </summary>
        public OperatingSystemType OS { get; set; }

        /// <summary>
        /// Gets or sets the architecture of the processing node's operating system.
        /// </summary>
        public ArchitectureType Architecture { get; set; }
        
        /// <summary>
        /// Gets or sets the number of runners on this node
        /// </summary>
        public int Runners { get; set; }
    
        /// <summary>
        /// Gets or sets if this is the internal processing node
        /// </summary>
        public bool Internal { get; set; }
    }
}
