using System.Text.Json;
using System.Text.RegularExpressions;
using FileFlows.DataLayer.DatabaseConnectors;
using FileFlows.DataLayer.Helpers;
using FileFlows.DataLayer.Models;
using FileFlows.Plugin;
using FileFlows.ServerShared.Helpers;
using FileFlows.ServerShared.Models;
using FileFlows.Shared;
using FileFlows.Shared.Json;
using FileFlows.Shared.Models;
using FileHelper = FileFlows.Plugin.Helpers.FileHelper;

namespace FileFlows.DataLayer;

/// <summary>
/// Manages data access operations for the LibraryFile table
/// </summary>
internal class DbLibraryFileManager : BaseManager
{
    /// <summary>
    /// Initializes a new instance of the LibraryFile manager
    /// </summary>
    /// <param name="logger">the logger</param>
    /// <param name="dbType">the type of database</param>
    /// <param name="dbConnector">the database connector</param>
    public DbLibraryFileManager(ILogger logger, DatabaseType dbType, IDatabaseConnector dbConnector)
        : base(logger, dbType, dbConnector)
    {
    }
    
    /// <summary>
    /// Converts a datetime to a string for the database
    /// </summary>
    /// <param name="date">the date to convert</param>
    /// <returns>the converted data as a string</returns>
    private string Date(DateTime date)
        =>  DbConnector.FormatDateQuoted(date);
    
    #region basic, used in migration

    /// <summary>
    /// Inserts a new LibraryFile
    /// </summary>
    /// <param name="item">the new LibraryFile</param>
    public Task Insert(LibraryFile item)
        => InsertBulk(new [] { item });

    private void EnsureValusAreAcceptable(LibraryFile file)
    {
        if (file.Uid == Guid.Empty)
            file.Uid = Guid.NewGuid();
        file.Fingerprint ??= string.Empty;
        file.FinalFingerprint ??= string.Empty;
        file.FlowName ??= string.Empty;
        file.NodeName ??= string.Empty;
        file.OutputPath ??= string.Empty;
        file.LibraryName ??= string.Empty;
        file.RelativePath ??= string.Empty;
        file.DuplicateName ??= string.Empty;
        file.FailureReason ??= string.Empty;

        if (file.DateCreated.Year < 2000)
            file.DateCreated = DateTime.UtcNow;
        
        file.DateModified = DateTime.UtcNow;
        file.HoldUntil = file.HoldUntil.EnsureNotLessThan1970();
        file.ProcessingStarted = file.ProcessingStarted.EnsureNotLessThan1970();
        file.ProcessingEnded = file.ProcessingEnded.EnsureNotLessThan1970();
    }

    /// <summary>
    /// Updates a Library File
    /// </summary>
    /// <param name="file">the LibraryFile to update</param>
    public async Task Update(LibraryFile file)
    {
        EnsureValusAreAcceptable(file);

        string strOriginalMetadata = JsonEncode(file.OriginalMetadata);
        string strFinalMetadata = JsonEncode(file.FinalMetadata);
        string strExecutedNodes = JsonEncode(file.ExecutedNodes);
        string strCustomVariables= JsonEncode(file.CustomVariables);
        string sql =
            $"update {Wrap(nameof(LibraryFile))} set " +
            $" {Wrap(nameof(LibraryFile.Name))} = @0, " +
            $" {Wrap(nameof(LibraryFile.RelativePath))}=@1, " +
            $" {Wrap(nameof(LibraryFile.Fingerprint))}=@2, " +
            $" {Wrap(nameof(LibraryFile.FinalFingerprint))}=@3, " +
            $" {Wrap(nameof(LibraryFile.IsDirectory))}=@4, " +
            $" {Wrap(nameof(LibraryFile.LibraryUid))}=@5, " +
            $" {Wrap(nameof(LibraryFile.LibraryName))}=@6, " +
            $" {Wrap(nameof(LibraryFile.FlowUid))}=@7, " +
            $" {Wrap(nameof(LibraryFile.FlowName))}=@8, " +
            $" {Wrap(nameof(LibraryFile.DuplicateUid))}=@9, " +
            $" {Wrap(nameof(LibraryFile.DuplicateName))}=@10, " +
            $" {Wrap(nameof(LibraryFile.NodeUid))}=@11, " +
            $" {Wrap(nameof(LibraryFile.NodeName))}=@12, " +
            $" {Wrap(nameof(LibraryFile.WorkerUid))}=@13, " +
            $" {Wrap(nameof(LibraryFile.ProcessOnNodeUid))}=@14, " +
            $" {Wrap(nameof(LibraryFile.OutputPath))}=@15, " +
            $" {Wrap(nameof(LibraryFile.NoLongerExistsAfterProcessing))}=@16, " +
            $" {Wrap(nameof(LibraryFile.OriginalMetadata))}=@17, " +
            $" {Wrap(nameof(LibraryFile.FinalMetadata))}=@18, " +
            $" {Wrap(nameof(LibraryFile.ExecutedNodes))}=@19, " +
            $" {Wrap(nameof(LibraryFile.CustomVariables))}=@20, " +
            $" {Wrap(nameof(LibraryFile.FailureReason))}=@21, " +
            $" {Wrap("ProcessingOrder")}={file.Order}, " + // special case, since Order is reserved in sql
            $" {Wrap(nameof(LibraryFile.Status))} = {(int)file.Status}, " +
            $" {Wrap(nameof(LibraryFile.Flags))} = {(int)file.Flags}, " +
            $" {Wrap(nameof(LibraryFile.OriginalSize))} = {file.OriginalSize}," +
            $" {Wrap(nameof(LibraryFile.FinalSize))} = {file.FinalSize}," +
            $" {Wrap(nameof(LibraryFile.DateCreated))} = {DbConnector.FormatDateQuoted(file.DateCreated)}," +
            $" {Wrap(nameof(LibraryFile.DateModified))} = {DbConnector.FormatDateQuoted(file.DateModified)}," +
            $" {Wrap(nameof(LibraryFile.CreationTime))}={DbConnector.FormatDateQuoted(file.CreationTime)}, " +
            $" {Wrap(nameof(LibraryFile.LastWriteTime))}={DbConnector.FormatDateQuoted(file.LastWriteTime)}, " +
            $" {Wrap(nameof(LibraryFile.HoldUntil))}={DbConnector.FormatDateQuoted(file.HoldUntil)}, " +
            $" {Wrap(nameof(LibraryFile.ProcessingStarted))}={DbConnector.FormatDateQuoted(file.ProcessingStarted)}, " +
            $" {Wrap(nameof(LibraryFile.ProcessingEnded))}={DbConnector.FormatDateQuoted(file.ProcessingEnded)} " +
            $" where {Wrap(nameof(LibraryFile.Uid))} = '{file.Uid}'";

        try
        {
            bool postgres = DbType == DatabaseType.Postgres;
            bool useDateTime = DbType is DatabaseType.Postgres or DatabaseType.SqlServer or DatabaseType.MySql;
            using var db = await DbConnector.GetDb(write: true);
            await db.Db.ExecuteAsync(sql,
                file.Name,
                file.RelativePath,
                file.Fingerprint, 
                file.FinalFingerprint,
                postgres ? file.IsDirectory : file.IsDirectory ? 1 : 0,
                file.LibraryUid?.ToString() ?? string.Empty,
                file.LibraryName,
                file.FlowUid?.ToString() ?? string.Empty,
                file.FlowName,
                file.DuplicateUid?.ToString() ?? string.Empty,
                file.DuplicateName,
                file.NodeUid?.ToString() ?? string.Empty,
                file.NodeName,
                file.WorkerUid?.ToString() ?? string.Empty,
                file.ProcessOnNodeUid?.ToString() ?? string.Empty,
                file.OutputPath,
                postgres ? file.NoLongerExistsAfterProcessing : file.NoLongerExistsAfterProcessing ? 1 : 0,
                strOriginalMetadata,
                strFinalMetadata,
                strExecutedNodes,
                strCustomVariables,
                file.FailureReason
            );
            //Logger.Instance.DLog("File: " + file.Name + "\nExecuted nodes: " + strExecutedNodes);
        }
        catch (Exception ex)
        {
            Logger.ELog($"Error updating library file: {ex.Message}" + Environment.NewLine + sql);
            throw;
        }

        // Database_Log(dt2,(update ? "update": "insert") + " object (actual)");
        // Database_Log(dt, update ? "updated object" : "insert object");
    }

    /// <summary>
    /// Bulk insert many files
    /// </summary>
    /// <param name="files">the files to insert</param>
    public async Task InsertBulk(LibraryFile[] files)
    {
        string? sql = null;
        try
        {
            using var db = await DbConnector.GetDb(write: true);
            db.Db.BeginTransaction();
            foreach (var file in files)
            {
                EnsureValusAreAcceptable(file);
                List<object> parameters = new();
                int offset = 0; //parameters.Count - 1;

                bool postgres = DbType == DatabaseType.Postgres;

                sql = $"insert into {Wrap(nameof(LibraryFile))} ( " +
                             $"{Wrap(nameof(LibraryFile.Uid))}, " +
                             $"{Wrap(nameof(LibraryFile.Name))}, " +
                             $"{Wrap(nameof(LibraryFile.Status))}, " +
                             $"{Wrap(nameof(LibraryFile.Flags))}, " +
                             $"{Wrap(nameof(LibraryFile.OriginalSize))}, " +
                             $"{Wrap(nameof(LibraryFile.FinalSize))}, " +
                             $"{Wrap("ProcessingOrder")}, " + // special case, since Order is reserved in sql
                             $"{Wrap(nameof(LibraryFile.IsDirectory))}, " +
                             $"{Wrap(nameof(LibraryFile.NoLongerExistsAfterProcessing))}, " +

                             $"{Wrap(nameof(LibraryFile.DateCreated))}, " +
                             $"{Wrap(nameof(LibraryFile.DateModified))}, " +
                             $"{Wrap(nameof(LibraryFile.CreationTime))}, " +
                             $"{Wrap(nameof(LibraryFile.LastWriteTime))}, " +
                             $"{Wrap(nameof(LibraryFile.HoldUntil))}, " +
                             $"{Wrap(nameof(LibraryFile.ProcessingStarted))}, " +
                             $"{Wrap(nameof(LibraryFile.ProcessingEnded))}, " +

                             $"{Wrap(nameof(LibraryFile.RelativePath))}, " +
                             $"{Wrap(nameof(LibraryFile.Fingerprint))}, " +
                             $"{Wrap(nameof(LibraryFile.FinalFingerprint))}, " +
                             $"{Wrap(nameof(LibraryFile.LibraryUid))}, " +
                             $"{Wrap(nameof(LibraryFile.LibraryName))}, " +
                             $"{Wrap(nameof(LibraryFile.FlowUid))}, " +
                             $"{Wrap(nameof(LibraryFile.FlowName))}, " +
                             $"{Wrap(nameof(LibraryFile.DuplicateUid))}, " +
                             $"{Wrap(nameof(LibraryFile.DuplicateName))}, " +
                             $"{Wrap(nameof(LibraryFile.NodeUid))}, " +
                             $"{Wrap(nameof(LibraryFile.NodeName))}, " +
                             $"{Wrap(nameof(LibraryFile.WorkerUid))}, " +
                             $"{Wrap(nameof(LibraryFile.ProcessOnNodeUid))}, " +
                             $"{Wrap(nameof(LibraryFile.OutputPath))}, " +
                             $"{Wrap(nameof(LibraryFile.OriginalMetadata))}, " +
                             $"{Wrap(nameof(LibraryFile.FinalMetadata))}, " +
                             $"{Wrap(nameof(LibraryFile.ExecutedNodes))}, " +
                             $"{Wrap(nameof(LibraryFile.CustomVariables))}, " +
                             $"{Wrap(nameof(LibraryFile.FailureReason))} " +
                             " )" +
                             $" values (@{offset++},@{offset++}," +
                             ((int)file.Status) + ", " +
                             ((int)file.Flags) + ", " +
                             (file.OriginalSize) + ", " +
                             (file.FinalSize) + ", " +
                             (file.Order) + ", " +
                             (file.IsDirectory ? (postgres ? "true" : "1") : (postgres ? "false" : "0")) + "," +
                             (file.NoLongerExistsAfterProcessing
                                 ? (postgres ? "true" : "1")
                                 : (postgres ? "false" : "0")) +
                             "," +

                             DbConnector.FormatDateQuoted(file.DateCreated) + "," + //$"@{++offset}," + // date created
                             DbConnector.FormatDateQuoted(file.DateModified) +
                             "," + //$"@{++offset}," + // date modified
                             DbConnector.FormatDateQuoted(file.CreationTime) + ", " +
                             DbConnector.FormatDateQuoted(file.LastWriteTime) + ", " +
                             DbConnector.FormatDateQuoted(file.HoldUntil) + ", " +
                             DbConnector.FormatDateQuoted(file.ProcessingStarted) + ", " +
                             DbConnector.FormatDateQuoted(file.ProcessingEnded) + ", " +
                             $"@{offset++},@{offset++},@{offset++},@{offset++},@{offset++},@{offset++}," +
                             $"@{offset++},@{offset++},@{offset++},@{offset++},@{offset++},@{offset++}," +
                             $"@{offset++},@{offset++},@{offset++},@{offset++},@{offset++},@{offset++},@{offset++});\n";

                parameters.Add(DbType is DatabaseType.Sqlite ? file.Uid.ToString() : file.Uid);
                parameters.Add(file.Name);

                // we have to always include every value for the migration, otherwise if we use default and the data is migrated that data will change
                parameters.Add(file.RelativePath);
                parameters.Add(file.Fingerprint);
                parameters.Add(file.FinalFingerprint ?? string.Empty);
                parameters.Add(file.LibraryUid?.ToString() ?? string.Empty);
                parameters.Add(file.LibraryName);
                parameters.Add(file.FlowUid?.ToString() ?? string.Empty);
                parameters.Add(file.FlowName ?? string.Empty);
                parameters.Add(file.DuplicateUid?.ToString() ?? string.Empty);
                parameters.Add(file.DuplicateName ?? string.Empty);
                parameters.Add(file.NodeUid?.ToString() ?? string.Empty);
                parameters.Add(file.NodeName ?? string.Empty);
                parameters.Add(file.WorkerUid?.ToString() ?? string.Empty);
                parameters.Add(file.ProcessOnNodeUid?.ToString() ?? string.Empty);
                parameters.Add(file.OutputPath ?? string.Empty);
                parameters.Add(JsonEncode(file.OriginalMetadata));
                parameters.Add(JsonEncode(file.FinalMetadata));
                parameters.Add(JsonEncode(file.ExecutedNodes));
                parameters.Add(JsonEncode(file.CustomVariables));
                parameters.Add(file.FailureReason ?? string.Empty);
                await db.Db.ExecuteAsync(sql, parameters.ToArray());
            }

            db.Db.CompleteTransaction();
        }
        catch (Exception ex)
        {
            Logger.ELog("Failed inserting library file: " + ex.Message + (string.IsNullOrWhiteSpace(sql) ? string.Empty : Environment.NewLine + sql));
        }
    }

    /// <summary>
    /// Fetches all items
    /// </summary>
    /// <returns>the items</returns>
    internal async Task<List<LibraryFile>> GetAll()
    {
        using var db = await DbConnector.GetDb();
        return await db.Db.FetchAsync<LibraryFile>();
    }

    /// <summary>
    /// Gets the total number of files in the database
    /// This is used for unit testing
    /// </summary>
    /// <returns>the total number of files</returns>
    internal async Task<int> GetTotal()
    {
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteScalarAsync<int>("select count(*) from " + Wrap(nameof(LibraryFile)));
    }
    #endregion
    
    #region getters

    /// <summary>
    /// Gets a library file by its UID
    /// </summary>
    /// <param name="uid">The UID of the library file</param>
    /// <returns>The library file if found, otherwise null</returns>
    public async Task<LibraryFile?> Get(Guid uid)
    {
        using var db = await DbConnector.GetDb();
        return await db.Db.FirstOrDefaultAsync<LibraryFile>($"where {Wrap(nameof(LibraryFile.Uid))}='{uid}'");
    }

    /// <summary>
    /// Gets a library file if it is known
    /// </summary>
    /// <param name="path">the path of the library file</param>
    /// <param name="libraryUid">[Optional] the UID of the library the file is in, if not passed in then the first file with the name will be used</param>
    /// <returns>the library file if it is known</returns>
    public async Task<LibraryFile?> GetFileIfKnown(string path, Guid? libraryUid)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        using var db = await DbConnector.GetDb();
        string where = "where ";
        if (libraryUid != null)
            where += $" {Wrap(nameof(LibraryFile.LibraryUid))} = '{libraryUid.Value}' and ";
        string whereOutput;
        string folder = FileHelper.GetDirectory(path);
        if (string.IsNullOrWhiteSpace(folder) == false)
        {
            whereOutput =
                $"({Wrap(nameof(LibraryFile.OutputPath))} = @0 and {Wrap(nameof(LibraryFile.OutputPath))} like @1)";
        }
        else
        {
            whereOutput = $"{Wrap(nameof(LibraryFile.OutputPath))} = @0";
        }

        string sql =
            $"select * from {Wrap(nameof(LibraryFile))} {where} ({Wrap(nameof(LibraryFile.Name))} = @0 or {whereOutput})";
        return await db.Db.FirstOrDefaultAsync<LibraryFile>(sql, path, folder + "%");
    }

    /// <summary>
    /// Gets a library file if it is known by its fingerprint
    /// </summary>
    /// <param name="libraryUid">The UID of the library</param>
    /// <param name="fingerprint">the fingerprint of the library file</param>
    /// <returns>the library file if it is known</returns>
    public async Task<LibraryFile?> GetFileByFingerprint(Guid libraryUid, string fingerprint)
    {
        string sql =
            $"select * from {Wrap(nameof(LibraryFile))} where {Wrap(nameof(LibraryFile.LibraryUid))} = '{libraryUid}' and " +
            $"( {Wrap(nameof(LibraryFile.Fingerprint))} = @0 or {Wrap(nameof(LibraryFile.FinalFingerprint))} = @0 )";
        try
        {
            using var db = await DbConnector.GetDb();
            return await db.Db.FirstOrDefaultAsync<LibraryFile>(sql, fingerprint);
        }
        catch (Exception ex)
        {
            Logger.ELog("Failed getting file by fingerprint: " + ex.Message + Environment.NewLine + sql);
            return null;
        }
    }

    #endregion
    
    #region deletes

    /// <summary>
    /// Deletes files from the database
    /// </summary>
    /// <param name="uids">the UIDs of the files to remove</param>
    public async Task Delete(params Guid[] uids)
    {
        if (uids?.Any() != true)
            return;
        
        string inStr = string.Join(",", uids.Select(x => $"'{x}'"));
        string sql = $"delete from  {Wrap(nameof(LibraryFile))} " +
                     $" where {Wrap(nameof(LibraryFile.Uid))} in ({inStr})";
        
        using var db = await DbConnector.GetDb();
        await db.Db.ExecuteAsync(sql);
    }
    
    /// <summary>
    /// Deletes files from the database
    /// </summary>
    /// <param name="libraryUids">the UIDs of the libraries to remove</param>
    public async Task DeleteByLibrary(params Guid[] libraryUids)
    {
        if (libraryUids?.Any() != true)
            return;   
        string inStr = string.Join(",", libraryUids.Select(x => $"'{x}'"));
        string sql = $"delete from  {Wrap(nameof(LibraryFile))} " +
                     $" where {Wrap(nameof(LibraryFile.LibraryUid))} in ({inStr})";
        
        using var db = await DbConnector.GetDb();
        await db.Db.ExecuteAsync(sql);
    }
    #endregion
    
    
    #region updates
    
    /// <summary>
    /// Sets a status on a file
    /// </summary>
    /// <param name="status">The status to set</param>
    /// <param name="uids">the UIDs of the files</param>
    /// <returns>true if any rows were updated, otherwise false</returns>
    internal async Task<bool> SetStatus(FileStatus status, params Guid[] uids)
    {
        if (uids?.Any() != true)
            return false;
        
        int iStatus = (int)status;
        if (iStatus < 0)
            iStatus = 0; // negative status are just special unprocessed statuses
        string hold = string.Empty;
        if (status == FileStatus.Unprocessed)
            hold = $", {Wrap(nameof(LibraryFile.HoldUntil))} = '1970-01-01 00:00:01'";
        
        string inStr = string.Join(",", uids.Select(x => $"'{x}'"));
        string sql = $"update {Wrap(nameof(LibraryFile))} " +
                     $" set {Wrap(nameof(LibraryFile.Status))} = {iStatus} " + hold + 
                     $" where {Wrap(nameof(LibraryFile.Uid))} in ({inStr})";
        
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteAsync(sql) > 0;
    }
    
    /// <summary>
    /// Clears the executed nodes, metadata, final size etc for a file
    /// </summary>
    /// <param name="uid">The UID of the file</param>
    /// <param name="flowUid">the UID of the flow that will be executed</param>
    /// <param name="flowName">the name of the flow that will be executed</param>
    /// <returns>true if a row was updated, otherwise false</returns>
    public async Task<bool> ResetFileInfoForProcessing(Guid uid, Guid? flowUid, string? flowName)
    {
        try
        {
            string sql = $"update {Wrap(nameof(LibraryFile))} set " +
                         $" {Wrap(nameof(LibraryFile.ExecutedNodes))} = '', " +
                         $" {Wrap(nameof(LibraryFile.OriginalMetadata))} = '', " +
                         $" {Wrap(nameof(LibraryFile.FinalMetadata))} = '', " +
                         $" {Wrap(nameof(LibraryFile.FinalSize))} = 0, " +
                         $" {Wrap(nameof(LibraryFile.OutputPath))} = '', " +
                         $" {Wrap(nameof(LibraryFile.FailureReason))} = '', " +
                         $" {Wrap(nameof(LibraryFile.ProcessOnNodeUid))} = '', " +
                         $" {Wrap(nameof(LibraryFile.FlowUid))} = '{flowUid?.ToString() ?? ""}', " +
                         $" {Wrap(nameof(LibraryFile.FlowName))} = @0, " +
                         $" {Wrap(nameof(LibraryFile.ProcessingEnded))} = " +
                         DbConnector.FormatDateQuoted(new DateTime(1970, 1, 1)) +
                         $" where {Wrap(nameof(LibraryFile.Uid))} = '{uid}'";

            using var db = await DbConnector.GetDb();
            return await db.Db.ExecuteAsync(sql, flowName ?? string.Empty) > 0;
        }
        catch (Exception ex)
        {
            Logger.WLog("Failed to rest file info: " + ex.Message);
            return false;
        }
    }
    
    
    /// <summary>
    /// Updates the original size of a file
    /// </summary>
    /// <param name="uid">The UID of the file</param>
    /// <param name="size">the size of the file in bytes</param>
    /// <returns>true if a row was updated, otherwise false</returns>
    public async Task<bool> UpdateOriginalSize(Guid uid, long size)
    {
        string sql = $"update {Wrap(nameof(LibraryFile))} set " +
                     $" {Wrap(nameof(LibraryFile.OriginalSize))} = {size} " +
                     $" where {Wrap(nameof(LibraryFile.Uid))} = '{uid}'";
        
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteAsync(sql) > 0;
    }
    
    
    /// <summary>
    /// Updates a flow name in the database
    /// </summary>
    /// <param name="uid">the UID of the flow</param>
    /// <param name="name">the updated name of the flow</param>
    /// <returns>true if any rows were updated, otherwise false</returns>
    public async Task<bool> UpdateFlowName(Guid uid, string name)
    {
        string sql = $"update {Wrap(nameof(LibraryFile))} set " +
                     $" {Wrap(nameof(LibraryFile.FlowName))} = @0 " +
                     $" where {Wrap(nameof(LibraryFile.FlowUid))} = '{uid}'";
        
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteAsync(sql, name) > 0;
    }

    /// <summary>
    /// Updates a library name in the database
    /// </summary>
    /// <param name="uid">the UID of the library</param>
    /// <param name="name">the updated name of the library</param>
    /// <returns>true if any rows were updated, otherwise false</returns>
    public async Task<bool> UpdateLibraryName(Guid uid, string name)
    {
        string sql = $"update {Wrap(nameof(LibraryFile))} set " +
                     $" {Wrap(nameof(LibraryFile.LibraryName))} = @0 " +
                     $" where {Wrap(nameof(LibraryFile.LibraryUid))} = '{uid}'";
        
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteAsync(sql, name) > 0;
    }
    
    /// <summary>
    /// Updates a node name in the database
    /// </summary>
    /// <param name="uid">the UID of the node</param>
    /// <param name="name">the updated name of the node</param>
    /// <returns>true if any rows were updated, otherwise false</returns>
    public async Task<bool> UpdateNodeName(Guid uid, string name)
    {
        string sql = $"update {Wrap(nameof(LibraryFile))} set " +
                     $" {Wrap(nameof(LibraryFile.NodeName))} = @0 " +
                     $" where {Wrap(nameof(LibraryFile.NodeUid))} = '{uid}'";
        
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteAsync(sql, name) > 0;
    }
    
    /// <summary>
    /// Force processing a set of files
    /// </summary>
    /// <param name="uids">the UIDs of the files</param>
    /// <returns>true if any rows were updated, otherwise false</returns>
    public async Task<bool> ForceProcessing(Guid[] uids)
    {
        if (uids?.Any() != true)
            return false;
        string inStr = string.Join(",", uids.Select(x => $"'{x}'"));
        string sql = $"update {Wrap(nameof(LibraryFile))} set " +
                     $" {Wrap(nameof(LibraryFile.Flags))} = {Wrap(nameof(LibraryFile.Flags))} | {((int)LibraryFileFlags.ForceProcessing)}" +
                     $" where {Wrap(nameof(LibraryFile.Uid))} in ({inStr})";
        
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteAsync(sql) > 0;
    }
    
    /// <summary>
    /// Toggles a flag on files
    /// </summary>
    /// <param name="flag">the flag to toggle</param>
    /// <param name="uids">the UIDs of the files</param>
    /// <returns>true if any rows were updated, otherwise false</returns>
    public async Task<bool> ToggleFlag(LibraryFileFlags flag, Guid[] uids)
    {
        if (uids?.Any() != true)
            return false;
        int iflag = (int)flag;
        string inStr = string.Join(",", uids.Select(x => $"'{x}'"));
        string sql = $"update {Wrap(nameof(LibraryFile))} set " +
                     $" {Wrap(nameof(LibraryFile.Flags))} = case " +
                     $" when {Wrap(nameof(LibraryFile.Flags))} & {iflag} > 0 then {Wrap(nameof(LibraryFile.Flags))} & ~{iflag} " +
                     $" else {Wrap(nameof(LibraryFile.Flags))} | {iflag} " +
                     $" end " +
                     $" where {Wrap(nameof(LibraryFile.Uid))} in ({inStr})";
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteAsync(sql) > 0;
    }
    
    /// <summary>
    /// Reprocess all files based on library UIDs
    /// </summary>
    /// <param name="libraryUids">an array of UID of the libraries to reprocess</param>
    /// <returns>true if any rows were updated, otherwise false</returns>
    public async Task<bool> ReprocessByLibraryUid(params Guid[] libraryUids)
    {
        if (libraryUids?.Any() != true)
            return false;
        
        string inStr = string.Join(",", libraryUids.Select(x => $"'{x}'"));
        string sql = $"update {Wrap(nameof(LibraryFile))} set " +
                     $" {Wrap(nameof(LibraryFile.Status))} = 0 " +
                     $" where {Wrap(nameof(LibraryFile.LibraryUid))} in ({inStr}) " +
                     $" and {Wrap(nameof(LibraryFile.Status))} <> {(int)FileStatus.Processing}";
        
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteAsync(sql) > 0;
    }
    #endregion
    
    #region get next file

    /// <summary>
    /// Updates a file as started processing
    /// </summary>
    /// <param name="uid">the UID of the file</param>
    /// <param name="nodeUid">the UID of the node processing this file</param>
    /// <param name="nodeName">the name of the node processing this file</param>
    /// <param name="workerUid">the UID of the worker processing this file</param>
    /// <returns>true if successfully updated, otherwise false</returns>
    public async Task<bool> StartProcessing(Guid uid, Guid nodeUid, string nodeName, Guid workerUid)
    {
        string sql = "update " + Wrap(nameof(LibraryFile)) + " set " +
                     Wrap(nameof(LibraryFile.NodeUid)) + $" = '{nodeUid}', " +
                     Wrap(nameof(LibraryFile.NodeName)) + " = @0, " +
                     Wrap(nameof(LibraryFile.WorkerUid)) + $" = '{workerUid}', " +
                     Wrap(nameof(LibraryFile.Status)) + $" = {(int)FileStatus.Processing}, " +
                     Wrap(nameof(LibraryFile.ProcessingStarted)) + " = " + DbConnector.FormatDateQuoted(DateTime.UtcNow) + ", " +
                     Wrap(nameof(LibraryFile.OriginalMetadata)) + " = '', " +
                     Wrap(nameof(LibraryFile.FinalMetadata)) + " = '', " +
                     Wrap(nameof(LibraryFile.ExecutedNodes)) + " = '' " +
                     "where " + Wrap(nameof(LibraryFile.Uid)) + $" = '{uid}'";
        
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteAsync(sql, nodeName)  > 0;
    }


    /// <summary>
    /// Constructs a next library file result
    /// </summary>
    /// <param name="status">the status of the call</param>
    /// <param name="file">the library file to process</param>
    /// <returns>the next library file result</returns>
    private NextLibraryFileResult NextFileResult(NextLibraryFileStatus? status = null, LibraryFile? file = null)
    {
        NextLibraryFileResult result = new();
        if (status != null)
            result.Status = status.Value;
        result.File = file;
        return result;
    }
    
    

    /// <summary>
    /// Gets the total items matching the filter
    /// </summary>
    /// <param name="filter">the filter</param>
    /// <returns>the total number of items matching</returns>
    public async Task<int> GetTotalMatchingItems(LibraryFileFilter filter)
    {
        string sql;
        try
        {
            List<string> wheres = new();
            if(filter.LibraryUid != null)
                wheres.Add($"{Wrap(nameof(LibraryFile.LibraryUid))} = '{filter.LibraryUid}'");
            if(string.IsNullOrWhiteSpace(filter.Filter) == false)
                wheres.Add($"lower({Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.Name))}) like lower('%{filter.Filter.Replace("'", "''").Replace(" ", "%")}%')");
            
            if(filter.Status == null)
            {
                using var db = await DbConnector.GetDb();
                sql =
                    $"select count({Wrap(nameof(LibraryFile.Uid))}) from {Wrap(nameof(LibraryFile))} " +
                    $"where " + string.Join(" and ", wheres);
                return await db.Db.ExecuteScalarAsync<int>(sql);
            }
            if ((int)filter.Status > 0)
            {
                wheres.Add($"{Wrap(nameof(LibraryFile.Status))} = {(int)filter.Status}");
                if(filter.NodeUid != null)
                    wheres.Add($"{Wrap(nameof(LibraryFile.NodeUid))} = '{filter.NodeUid}'");
                if(filter.FlowUid != null)
                    wheres.Add($"{Wrap(nameof(LibraryFile.FlowUid))} = '{filter.FlowUid}'");
                sql = $"select count({Wrap(nameof(LibraryFile.Uid))}) " +
                      $"from {Wrap(nameof(LibraryFile))} " +
                      $"where " + string.Join(" and ", wheres);
                using var db = await DbConnector.GetDb();
                return await db.Db.ExecuteScalarAsync<int>(sql);
            }

            var disabled = string.Join(", ",
                filter.SysInfo.AllLibraries.Values.Where(x => x.Enabled == false).Select(x => "'" + x.Uid + "'"));
            int quarter = TimeHelper.GetCurrentQuarter();
            var outOfSchedule = string.Join(", ",
                filter.SysInfo.AllLibraries.Values.Where(x => x.Schedule?.Length != 672 || x.Schedule[quarter] == '0').Select(x => "'" + x.Uid + "'"));
            
            wheres.Insert(0, $"{Wrap(nameof(LibraryFile.Status))} = {(int)FileStatus.Unprocessed}");

            sql = $"select count(*) from {Wrap(nameof(LibraryFile))}";
            
            // add disabled condition
            if (string.IsNullOrEmpty(disabled) == false)
                wheres.Add($" {Wrap(nameof(LibraryFile.LibraryUid))} {(filter.Status == FileStatus.Disabled ? "" : "not")} in ({disabled})");
            
            if (filter.Status == FileStatus.Disabled)
            {
                if (string.IsNullOrEmpty(disabled))
                    return 0;
                sql = sql + $"where " + string.Join(" and ", wheres);
                using var db = await DbConnector.GetDb();
                return await db.Db.ExecuteScalarAsync<int>(sql);
            }
            
            // add out of schedule condition
            if(string.IsNullOrEmpty(outOfSchedule) == false)
                wheres.Add($"{Wrap(nameof(LibraryFile.LibraryUid))} {(filter.Status == FileStatus.OutOfSchedule ? "" : "not")} in ({outOfSchedule})");
            
            if (filter.Status == FileStatus.OutOfSchedule)
            {
                if (string.IsNullOrEmpty(outOfSchedule))
                    return 0; // no out of schedule libraries
                sql = sql + $"where " + string.Join(" and ", wheres);
                using var db = await DbConnector.GetDb();
                return await db.Db.ExecuteScalarAsync<int>(sql);
            }
            
            // add on hold condition
            wheres.Add($"{Wrap(nameof(LibraryFile.HoldUntil))} {(filter.Status == FileStatus.OnHold ? ">" : "<=")} " +
                   DbConnector.FormatDateQuoted(DateTime.UtcNow));
            if (filter.Status == FileStatus.OnHold)
            {
                sql = sql + $"where " + string.Join(" and ", wheres);
                using var db = await DbConnector.GetDb();
                return await db.Db.ExecuteScalarAsync<int>(sql);
            }

            string libraryJoin = $"left join {Wrap(nameof(DbObject))}  on {Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Type))}" +
                                 $" = 'FileFlows.Shared.Models.Library' and {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.LibraryUid))} " +
                                 $" = {Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Uid))} ";
    
            sql = sql.Replace($" from {Wrap(nameof(LibraryFile))}", $" from {Wrap(nameof(LibraryFile))} " + libraryJoin);
            sql = sql + $"where " + string.Join(" and ", wheres);
            {
                using var db = await DbConnector.GetDb();
                return await db.Db.ExecuteScalarAsync<int>(sql);
            }
        }
        catch (Exception ex)
        {
            Logger.ELog("Failed GetTotalMatchingItems Files: " + ex.Message + "\n" + ex.StackTrace);
            return 0;
        }
    }


    /// <summary>
    /// Gets all the UIDs for library files in the system
    /// </summary>
    /// <returns>the UIDs of known library files</returns>
    public async Task<List<Guid>> GetUids()
    {
        using var db = await DbConnector.GetDb();
        return await db.Db.FetchAsync<Guid>($"select {Wrap(nameof(LibraryFile.Uid))} from {Wrap(nameof(LibraryFile))}");
    }
    
    /// <summary>
    /// Gets all matching library files
    /// </summary>
    /// <param name="filter">the filter to get files for</param>
    /// <returns>a list of matching library files</returns>
    public async Task<List<LibraryFile>> GetAll(LibraryFileFilter filter)
    {
        var sql = await ConstructQuery(filter);
        if (string.IsNullOrWhiteSpace(sql))
            return new List<LibraryFile>();
        if (filter.Skip > 0 || filter.Rows > 0)
        {
            sql += DbType switch
            {
                DatabaseType.MySql => filter.Rows > 0 ? $" LIMIT {filter.Skip}, {filter.Rows}" : $" LIMIT {filter.Rows} OFFSET {filter.Skip}",
                DatabaseType.Postgres => $" OFFSET {filter.Skip} LIMIT {filter.Rows}",
                DatabaseType.Sqlite => $" LIMIT {filter.Rows} OFFSET {filter.Skip}",
                DatabaseType.SqlServer => $" OFFSET {filter.Skip} ROWS FETCH NEXT {(filter.Rows > 0 ? filter.Rows : int.MaxValue)} ROWS ONLY",
                _ => string.Empty
            };
        }
        
        using var db = await DbConnector.GetDb();
        return await db.Db.FetchAsync<LibraryFile>(sql);
    }

    /// <summary>
    /// Constructs the query of the cached data
    /// </summary>
    /// <returns>a IEnumerable of files</returns>
    private async Task<string> ConstructQuery(LibraryFileFilter args)
    {
        try
        {
            if (args.AllowedLibraries is { Count: 0 })
                return string.Empty; // no libraries allowed 

            string AND_NOT_FORCED =
                $" and {Wrap(nameof(LibraryFile.Flags))} & {(int)LibraryFileFlags.ForceProcessing} = 0";

            string sql;
            List<string> orderBys = new();
            
            string ReturnWithOrderBy()
            {
#if(DEBUG)
                // make it easier to read for debugging
                sql = Regex.Replace(sql, @"\s+", " ");
                foreach (var keyword in new[] { "and", "where", "inner" })
                {
                    sql = sql.Replace($" {keyword} ", $"\n {keyword} ");
                }
#endif
                if (sql.StartsWith("select") == false)
                    sql = $"select {Wrap(nameof(LibraryFile))}.* from {Wrap(nameof(LibraryFile))} " + sql;
                orderBys = orderBys.Where(x => string.IsNullOrWhiteSpace(x) == false).ToList();
                return sql + (orderBys.Any() == false ? "" : "order by \n" + string.Join(", \n", orderBys));
            }

            int iStatus = 0;

            // the status in the db is correct and not a computed status
            if (args.Status == null)
            {
                sql = "where 1 = 1 "; // need something to start the where
            }
            else
            {
                iStatus = (int)args.Status;
                if (iStatus < 0)
                    iStatus = 0;
                sql = $"where {Wrap(nameof(LibraryFile.Status))} = {iStatus} ";
            }

            if (args.ForcedOnly)
                sql += $" and {Wrap(nameof(LibraryFile.Flags))} & {(int)LibraryFileFlags.ForceProcessing} > 0";

            if (string.IsNullOrWhiteSpace(args.Filter) == false)
            {
                var filter = args.Filter.ToLowerInvariant();
                sql += $" and lower({Wrap(nameof(LibraryFile.RelativePath))}) like " +
                       SqlHelper.Escape("%" + filter + "%");
            }

            if (args.LibraryUid != null)
                sql += $" and {Wrap(nameof(LibraryFile.LibraryUid))} = '{args.LibraryUid.Value}'";
            
            if (iStatus > 0)
            {
                if (args.SortBy != null)
                {
                    switch (args.SortBy)
                    {
                        case FilesSortBy.Size:
                            orderBys.Add(Wrap(nameof(LibraryFile.OriginalSize)));
                            break;
                        case FilesSortBy.SizeDesc:
                            orderBys.Add(Wrap(nameof(LibraryFile.OriginalSize)) + " desc");
                            break;
                        case FilesSortBy.Savings:
                            orderBys.Add(
                                $"case when {Wrap(nameof(LibraryFile.FinalSize))} > 0 then {Wrap(nameof(LibraryFile.OriginalSize))} - {Wrap(nameof(LibraryFile.FinalSize))} else {long.MaxValue} end");
                            break;
                        case FilesSortBy.SavingsDesc:
                            orderBys.Add(
                                $"case when {Wrap(nameof(LibraryFile.FinalSize))} > 0 then ({Wrap(nameof(LibraryFile.OriginalSize))} - {Wrap(nameof(LibraryFile.FinalSize))}) * -1 else 0 end");
                            break;
                        case FilesSortBy.Time:
                            switch (DbConnector.Type)
                            {
                                case DatabaseType.MySql:
                                    orderBys.Add(
                                        $"TIMESTAMPDIFF(SECOND, {Wrap(nameof(LibraryFile.ProcessingStarted))}, {Wrap(nameof(LibraryFile.ProcessingEnded))})");
                                    break;
                                case DatabaseType.Postgres:
                                    orderBys.Add(
                                        $"EXTRACT(EPOCH FROM {Wrap(nameof(LibraryFile.ProcessingEnded))} - {Wrap(nameof(LibraryFile.ProcessingStarted))})");
                                    break;
                                case DatabaseType.Sqlite:
                                    orderBys.Add(
                                        $"julianday({Wrap(nameof(LibraryFile.ProcessingEnded))}) - julianday({Wrap(nameof(LibraryFile.ProcessingStarted))})");
                                    break;
                                case DatabaseType.SqlServer:
                                    orderBys.Add(
                                        $"DATEDIFF(second, {Wrap(nameof(LibraryFile.ProcessingStarted))}, {Wrap(nameof(LibraryFile.ProcessingEnded))})");
                                    break;
                            }

                            break;
                        case FilesSortBy.TimeDesc:
                            switch (DbConnector.Type)
                            {
                                case DatabaseType.MySql:
                                    orderBys.Add(
                                        $"TIMESTAMPDIFF(SECOND, {Wrap(nameof(LibraryFile.ProcessingStarted))}, {Wrap(nameof(LibraryFile.ProcessingEnded))}) DESC");
                                    break;
                                case DatabaseType.Postgres:
                                    orderBys.Add(
                                        $"EXTRACT(EPOCH FROM {Wrap(nameof(LibraryFile.ProcessingEnded))} - {Wrap(nameof(LibraryFile.ProcessingStarted))}) DESC");
                                    break;
                                case DatabaseType.Sqlite:
                                    orderBys.Add(
                                        $"julianday({Wrap(nameof(LibraryFile.ProcessingEnded))}) - julianday({Wrap(nameof(LibraryFile.ProcessingStarted))}) DESC");
                                    break;
                                case DatabaseType.SqlServer:
                                    orderBys.Add(
                                        $"DATEDIFF(second, {Wrap(nameof(LibraryFile.ProcessingStarted))}, {Wrap(nameof(LibraryFile.ProcessingEnded))}) DESC");
                                    break;
                            }
                            break;
                    }
                }
                
                if (args.NodeUid != null)
                    sql += $" and {Wrap(nameof(LibraryFile.NodeUid))} = '{args.NodeUid.Value}'";
                
                if(args.ProcessingNodeUid != null)
                    sql += $" and ( {Wrap(nameof(LibraryFile.NodeUid))} = '{args.ProcessingNodeUid.Value}' or {Wrap(nameof(LibraryFile.NodeUid))} = '')";
                
                if (args.FlowUid != null)
                    sql += $" and {Wrap(nameof(LibraryFile.FlowUid))} = '{args.FlowUid.Value}'";

                if (args.Status is FileStatus.Processed or FileStatus.ProcessingFailed)
                {
                    orderBys.Add($" case " +
                                 $" when {Wrap(nameof(LibraryFile.ProcessingEnded))} > {Wrap(nameof(LibraryFile.ProcessingStarted))} THEN {Wrap(nameof(LibraryFile.ProcessingEnded))} " +
                                 $" else {Wrap(nameof(LibraryFile.ProcessingStarted))} " +
                                 $" end desc");
                    orderBys.Add($"{Wrap(nameof(LibraryFile.DateModified))} desc");
                }
                else
                    orderBys.Add($" {Wrap(nameof(LibraryFile.DateModified))} desc");


                return ReturnWithOrderBy();
            }


            var disabled = args.SysInfo.AllLibraries.Values.Where(x => x.Enabled == false)
                .Select(x => x.Uid).ToList();
            if (args.Status == FileStatus.Disabled)
            {
                if (disabled?.Any() != true)
                    return string.Empty; // no disabled libraries, therefore no disabled files
                // we don't want forced files
                sql += AND_NOT_FORCED;

                string libInStr = string.Join(",", disabled.Select(x => $"'{x}'"));
                sql += $" and {Wrap(nameof(LibraryFile.LibraryUid))} in ({libInStr})";
                orderBys.Add($"{Wrap(nameof(LibraryFile.DateModified))}");
                return ReturnWithOrderBy();
            }

            int quarter = TimeHelper.GetCurrentQuarter();
            var outOfSchedule = args.SysInfo.AllLibraries.Values
                .Where(x => x.Schedule?.Length != 672 || x.Schedule[quarter] == '0')
                .Select(x => x.Uid).Where(x => disabled.Contains(x) == false).ToList();
            if (args.Status == FileStatus.OutOfSchedule)
            {
                if (outOfSchedule?.Any() != true)
                    return string.Empty; // no out of schedule libraries, therefore no data
                // we don't want forced files
                sql += AND_NOT_FORCED;
                string libInStr = string.Join(",", outOfSchedule.Select(x => $"'{x}'"));
                sql += $" and {Wrap(nameof(LibraryFile.LibraryUid))} in ({libInStr})";
                orderBys.Add($"{Wrap(nameof(LibraryFile.DateModified))}");
                return ReturnWithOrderBy();
            }


            var maxedOutLibraries = args.GettingFileForProcess
                ? args.SysInfo.AllLibraries.Where(lib =>
                {
                    if(args.SysInfo.LicensedForProcessingOrder == false)
                        return false;
                    if (lib.Value.MaxRunners < 1)
                        return false; // no limit
                    int count = args.SysInfo.Executors.Count(exe => exe.Library.Uid == lib.Value.Uid);
                    return count >= lib.Value.MaxRunners;
                }).Select(x => x.Value.Uid).ToList()
                : new();


            if (outOfSchedule.Any() || disabled.Any() || maxedOutLibraries.Any())
            {
                string unwantedLibraries = string.Join(",",
                    disabled.Union(outOfSchedule).Union(maxedOutLibraries).Distinct().Select(x => $"'{x}'"));
                sql += $" and ( {Wrap(nameof(LibraryFile.Flags))} & {(int)LibraryFileFlags.ForceProcessing} > 0 or " +
                       $" {Wrap(nameof(LibraryFile.LibraryUid))} not in ({unwantedLibraries}) ) ";
            }

            if (args.Status == FileStatus.OnHold)
            {
                sql += $" and {Wrap(nameof(LibraryFile.HoldUntil))} > {Date(DateTime.UtcNow)} ";
                orderBys.Add($"{Wrap(nameof(LibraryFile.HoldUntil))}");
                orderBys.Add($"{Wrap(nameof(LibraryFile.DateModified))}");
                return ReturnWithOrderBy();
            }

            if (args.AllowedLibraries?.Any() == true)
            {
                string alllowedLibraries = string.Join(",", args.AllowedLibraries.Select(x => $"'{x}'"));
                sql += $" and {Wrap(nameof(LibraryFile.LibraryUid))} in ({alllowedLibraries}) ";
            }

            sql += $" and {Wrap(nameof(LibraryFile.HoldUntil))} <= {Date(DateTime.UtcNow)} ";

            if (args.MaxSizeMBs is > 0)
                sql += $" and {Wrap(nameof(LibraryFile.OriginalSize))} < " + args.MaxSizeMBs * 1_000_000 + " ";

            if (args.ExclusionUids?.Any() == true)
            {
                string unwanted = string.Join(",", args.ExclusionUids.Select(x => $"'{x}'"));
                sql += $" and {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.Uid))} not in ({unwanted}) ";
            }

            orderBys.Add($"case " +
                         $" when {Wrap("Processing" + nameof(LibraryFile.Order))} > 0 THEN {Wrap("Processing" + nameof(LibraryFile.Order))} " +
                         $" else {1_000_000_000} " +
                         $" end");


            var possibleComplexSortingLibraries = args.SysInfo.AllLibraries.Values.Where(library =>
            {
                if (library.Enabled == false)
                    return false;
                if (library.ProcessingOrder == ProcessingOrder.AsFound)
                    return false;
                if (args.AllowedLibraries?.Any() == true && args.AllowedLibraries.Contains(library.Uid) == false)
                    return false;
                if (TimeHelper.InSchedule(library.Schedule) == false)
                    return false;
                // check this library has any unprocessed files
                return true;
            }).Select(x => x.Uid)?.ToArray() ?? new Guid[] { };
            

            sql = $"select {Wrap(nameof(LibraryFile))}.* from {Wrap(nameof(LibraryFile))} " +
                  $" left outer join {Wrap(nameof(DbObject))} on " +
                  $" {Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Type))} = '{typeof(Library).FullName}' and " +
                  $" {Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Uid))} = " +
                  (DbType != DatabaseType.Postgres
                      ? $" {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.LibraryUid))} "
                      : $" cast({Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.LibraryUid))} as uuid) "
                  ) + sql;
            
            orderBys.Add(OrderByLibraryPriority());

            if (possibleComplexSortingLibraries.Any() == false || args.SysInfo.LicensedForProcessingOrder == false)
            {
                orderBys.Add($"{Wrap(nameof(LibraryFile.DateCreated))}");
                return ReturnWithOrderBy();
            }
            
            // check if any of the complex sorting libraries have any unprocessed files
            string inStr = string.Join(",", possibleComplexSortingLibraries.Select(x => $"'{x}'"));
            using var db = await DbConnector.GetDb();
            int unprocessedFiles = await db.Db.ExecuteScalarAsync<int>("select count(*) from " + Wrap(nameof(LibraryFile)) + " where " +
                                  Wrap(nameof(LibraryFile.Status)) + " = 0 and " +
                                  Wrap(nameof(LibraryFile.LibraryUid)) + $" in ({inStr})");
            
            if(unprocessedFiles < 1)
            {
                // no need to do complex sorting, no files match that
                orderBys.Add($"{Wrap(nameof(LibraryFile.DateCreated))}");
                return ReturnWithOrderBy();
            }

            switch (DbType)
            {
                case DatabaseType.MySql:
                    orderBys.Add($@" 
case 
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.Random} then cast(rand() as decimal)
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.SmallestFirst} then cast({Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.OriginalSize))} as decimal)
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.LargestFirst} then cast({Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.OriginalSize))} as decimal) * -1
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.NewestFirst} then cast(TIMESTAMPDIFF(SECOND, '1970-01-01', {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.CreationTime))}) as decimal) * -1
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.OldestFirst} then cast(TIMESTAMPDIFF(SECOND, '1970-01-01', {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.CreationTime))}) as decimal)
    else null 
end ");
                    orderBys.Add($@"
case 
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.Alphabetical} then {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.RelativePath))}
    else null
end");
                    break;
                case DatabaseType.Sqlite:
                    orderBys.Add($@" 
case 
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.Random} then random()
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.SmallestFirst} then {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.OriginalSize))}
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.LargestFirst} then {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.OriginalSize))} * -1 
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.NewestFirst} then strftime('%s', {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.CreationTime))}) * -1
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.OldestFirst} then {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.CreationTime))} 
    else null 
end ");
                    orderBys.Add($@"
case 
    when json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.Alphabetical} then {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.RelativePath))}
    else null
end");
                    break;
                case DatabaseType.Postgres:
                    orderBys.Add($@"
case 
    WHEN {Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}::json->>'{nameof(Library.ProcessingOrder)}' = '{(int)ProcessingOrder.Random}' then random()
    WHEN {Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}::json->>'{nameof(Library.ProcessingOrder)}' = '{(int)ProcessingOrder.SmallestFirst}' then {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.OriginalSize))} 
    WHEN {Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}::json->>'{nameof(Library.ProcessingOrder)}' = '{(int)ProcessingOrder.LargestFirst}' then {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.OriginalSize))} * -1
    WHEN {Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}::json->>'{nameof(Library.ProcessingOrder)}' = '{(int)ProcessingOrder.NewestFirst}' then extract(EPOCH FROM {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.CreationTime))}) * -1
    WHEN {Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}::json->>'{nameof(Library.ProcessingOrder)}' = '{(int)ProcessingOrder.OldestFirst}' then extract(EPOCH FROM {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.CreationTime))}) 
    else null
end ");
                    orderBys.Add(@$"
case 
    WHEN {Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}::json->>'{nameof(Library.ProcessingOrder)}' = '{(int)ProcessingOrder.Alphabetical}' then {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.RelativePath))} 
    else null 
end");
                    break;

                case DatabaseType.SqlServer:
                    orderBys.Add($@" 
case 
    when json_value({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.Random} then CAST(CHECKSUM(NEWID()) AS FLOAT)
    when json_value({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.SmallestFirst} then {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.OriginalSize))}
    when json_value({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.LargestFirst} then {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.OriginalSize))} * -1
    when json_value({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.NewestFirst} then DATEDIFF(second, '1970-01-01', {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.CreationTime))}) * -1
    when json_value({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.OldestFirst} then DATEDIFF(second, '1970-01-01', {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.CreationTime))})
    else null
end ");
                    orderBys.Add($@"
case
    when json_value({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.ProcessingOrder)}') = {(int)ProcessingOrder.Alphabetical} then {Wrap(nameof(LibraryFile))}.{Wrap(nameof(LibraryFile.RelativePath))}
    else null
end ");
                    break;
            }

            orderBys.Add($"{Wrap(nameof(LibraryFile.DateCreated))}");

            return ReturnWithOrderBy();
        }
        catch (Exception ex)
        {
            Logger.ELog("Failed GetAll Files: " + ex.Message + "\n" + ex.StackTrace);
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the order by SQL for library priority
    /// </summary>
    /// <returns>the order by SQL</returns>
    private string OrderByLibraryPriority()
    {
        switch (DbType)
        {
            case DatabaseType.SqlServer:
                return $@"cast(json_value({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.Priority)}') as int) desc";
            case DatabaseType.MySql:
                return $@"json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.Priority)}') desc";
            case DatabaseType.Sqlite:
                return $@"json_extract({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}, '$.{nameof(Library.Priority)}') desc";
            case DatabaseType.Postgres:
                return $@"cast({Wrap(nameof(DbObject))}.{Wrap(nameof(DbObject.Data))}::json->>'{nameof(Library.Priority)}' as int) desc";
        }

        return string.Empty;
    }

    #endregion
    
    #region get overview
    /// <summary>
    /// Gets the library status overview
    /// </summary>
    /// <returns>the library status overview</returns>
    public async Task<List<LibraryStatus>> GetStatus(List<Library> libraries)
    {
        var disabled = libraries.Where(x => x.Enabled == false).Select(x => x.Uid).ToList();
        List<Guid> libraryUids = libraries.Select(x => x.Uid).ToList();
        int quarter = TimeHelper.GetCurrentQuarter();
        var outOfSchedule = libraries.Where(x => disabled.Contains(x.Uid) == false && x.Schedule?.Length != 672 || x.Schedule[quarter] == '0')
            .Select(x => x.Uid).ToList();

        string AND_NOT_FORCED =
            $" and {Wrap(nameof(LibraryFile.Flags))} & {(int)LibraryFileFlags.ForceProcessing} = 0";
        
        using var db = await DbConnector.GetDb();

        string sql = $@"select {Wrap(nameof(LibraryFile.Status))}, count(*) AS {Wrap("StatusCount")}
from {Wrap(nameof(LibraryFile))}
where {Wrap(nameof(LibraryFile.Status))} > 0
group by {Wrap(nameof(LibraryFile.Status))}";
        var results = db.Db.Fetch<LibraryStatus>(sql);
        
        // now for the complicated bit
        
        if (disabled.Any())
        {
            string inStr = string.Join(",", disabled.Select(x => $"'{x}'"));
            
            var disabledCount = await db.Db.ExecuteScalarAsync<int>($@"select count(*)
from {Wrap(nameof(LibraryFile))}
where {Wrap(nameof(LibraryFile.Status))} = 0
{AND_NOT_FORCED}
and {Wrap(nameof(LibraryFile.LibraryUid))} in ({inStr})
");
            results.Add(new () { Count = disabledCount, Status = FileStatus.Disabled});
        }
        
        
        if (outOfSchedule.Any())
        {
            string inStr = string.Join(",", outOfSchedule.Select(x => $"'{x}'"));
            
            var disabledCount = await db.Db.ExecuteScalarAsync<int>($@"select count(*)
from {Wrap(nameof(LibraryFile))}
where {Wrap(nameof(LibraryFile.Status))} = 0
{AND_NOT_FORCED}
and {Wrap(nameof(LibraryFile.LibraryUid))} in ({inStr})
");
            results.Add(new () { Count = disabledCount, Status = FileStatus.OutOfSchedule});
        }

        string disabledOutOfScheduled = string.Join(",", disabled.Union(outOfSchedule).Select(x => $"'{x}'"));

        string FORCED_OR_LIBRARY = disabledOutOfScheduled == ""
            ? string.Empty
            : $" and ( ({Wrap(nameof(LibraryFile.Flags))} & {(int)LibraryFileFlags.ForceProcessing} > 0) or " +
              $"({Wrap(nameof(LibraryFile.LibraryUid))} not in ({disabledOutOfScheduled})) )";
        
        string onHoldCountSql = $@"select count(*)
from {Wrap(nameof(LibraryFile))}
where {Wrap(nameof(LibraryFile.Status))} = 0
and {Wrap(nameof(LibraryFile.HoldUntil))} > {Date(DateTime.UtcNow)}
{FORCED_OR_LIBRARY}";
        var onHoldCount = await db.Db.ExecuteScalarAsync<int>(onHoldCountSql);
        results.Add(new () { Count = onHoldCount, Status = FileStatus.OnHold});

        string sqlUnprocesed = $@"select count(*)
from {Wrap(nameof(LibraryFile))}
where {Wrap(nameof(LibraryFile.Status))} = 0
and {Wrap(nameof(LibraryFile.HoldUntil))} <= {Date(DateTime.UtcNow)}
{FORCED_OR_LIBRARY}
";
        var unProcessedCount = await db.Db.ExecuteScalarAsync<int>(sqlUnprocesed);
        results.Add(new () { Count = unProcessedCount, Status = FileStatus.Unprocessed});

        return results;
    }
    #endregion
    
    /// <summary>
    /// Gets the serialize options for Library Files
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
    {
        Converters = { new TimeSpanConverter() }
    };

    /// <summary>
    /// JSON encodes an object for the database
    /// </summary>
    /// <param name="o">the object to encode</param>
    /// <returns>the JSON encoded object</returns>
    private static string JsonEncode(object? o)
    {
        if (o == null)
            return string.Empty;
        return JsonSerializer.Serialize(o, JsonOptions);
    }

    /// <summary>
    /// Gets the processing time for each library file 
    /// </summary>
    /// <returns>the processing time for each library file</returns>
    public async Task<List<LibraryFileProcessingTime>> GetLibraryProcessingTimes()
    {
        string sql = @$"select 
{Wrap(nameof(LibraryFile.LibraryName))} as {Wrap(nameof(LibraryFileProcessingTime.Library))},
{Wrap(nameof(LibraryFile.OriginalSize))}, " +
                     DbConnector.TimestampDiffSeconds(nameof(LibraryFile.ProcessingStarted), nameof(LibraryFile.ProcessingEnded), nameof(LibraryFileProcessingTime.Seconds)) + 
                     $@" from {Wrap(nameof(LibraryFile))} 
where {Wrap(nameof(LibraryFile.Status))} = 1 and {Wrap(nameof(LibraryFile.ProcessingEnded))} > {Wrap(nameof(LibraryFile.ProcessingStarted))}";

        using var db = await DbConnector.GetDb();
        return await db.Db.FetchAsync<LibraryFileProcessingTime>(sql);
    }
    
    /// <summary>
    /// Resets any currently processing library files 
    /// This will happen if a server or node is reset
    /// </summary>
    /// <param name="nodeUid">[Optional] the UID of the node</param>
    /// <returns>true if any files were updated</returns>
    public async Task<bool> ResetProcessingStatus(Guid? nodeUid)
    {
        string sql =
            $"update {Wrap(nameof(LibraryFile))} set {Wrap(nameof(LibraryFile.Status))} = 0 where {Wrap(nameof(LibraryFile.Status))} = {(int)FileStatus.Processing}";
        if (nodeUid != null && nodeUid != Guid.Empty)
            sql += $" and {Wrap(nameof(LibraryFile.NodeUid))} = '{nodeUid}'";
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteAsync(sql) > 0;
    }

    /// <summary>
    /// Gets the current status of a file
    /// </summary>
    /// <param name="uid">The UID of the file</param>
    /// <returns>the current status of the file</returns>
    public async Task<FileStatus?> GetFileStatus(Guid uid)
    {
        using var db = await DbConnector.GetDb();
        var istatus = await db.Db.ExecuteScalarAsync<int?>("select " + Wrap(nameof(LibraryFile.Status)) + " from " + Wrap(nameof(LibraryFile)) +
                                  " where " + Wrap(nameof(LibraryFile.Uid)) + $" = '{uid}'");
        if (istatus == null)
            return null;
        return (FileStatus)istatus.Value;
    }

    // dont want to have to update the db while a file is processing, this adds too much strain
    // /// <summary>
    // /// Special case used by the flow runner to update a processing library file
    // /// </summary>
    // /// <param name="file">the processing library file</param>
    // public async Task UpdateWork(LibraryFile file)
    // {
    //     if (file == null)
    //         return;
    //     
    //     string sql = $"update {Wrap(nameof(LibraryFile))} set " +
    //                  $" {Wrap(nameof(LibraryFile.Status))} = {((int)file.Status)}, " + 
    //                  $" {Wrap(nameof(LibraryFile.FinalSize))} = {file.FinalSize}, " +
    //                  (file.Node == null ? "" : (
    //                      $" {Wrap(nameof(LibraryFile.NodeUid))} = '{file.NodeUid}', {Wrap(nameof(LibraryFile.NodeName))} = '{file.NodeName.Replace("'", "''")}', "
    //                  )) +
    //                  $" {Wrap(nameof(LibraryFile.WorkerUid))} = '{file.WorkerUid}', " +
    //                  $" {Wrap(nameof(LibraryFile.ProcessingStarted))} = {DbConnector.FormatDateQuoted(file.ProcessingStarted)}, " +
    //                  $" {Wrap(nameof(LibraryFile.ProcessingEnded))} = {DbConnector.FormatDateQuoted(file.ProcessingEnded)}, " +
    //                  $" {Wrap(nameof(LibraryFile.WorkerUid))} = @0 " +
    //                  (file.Status != FileStatus.Processing ? $", {Wrap(nameof(LibraryFile.Flags))} = 0 " : string.Empty) + // clear flags on processed files
    //                  $" where {Wrap(nameof(LibraryFile.Uid))} = '{file.Uid}'";
    //     
    //     string executedJson = file.ExecutedNodes?.Any() != true
    //         ? string.Empty
    //         : JsonSerializer.Serialize(file.ExecutedNodes, CustomDbMapper.JsonOptions);
    //     
    //     using var db = await DbConnector.GetDb();
    //     await db.Db.ExecuteAsync(sql, executedJson);
    // }

    /// <summary>
    /// Moves the passed in UIDs to the top of the processing order
    /// </summary>
    /// <param name="uids">the UIDs to move</param>
    public async Task MoveToTop(Guid[] uids)
    {
        if (uids?.Any() != true)
            return;
        string strUids = string.Join(", ", uids.Select(x => "'" + x + "'"));
        // get existing order first so we can shift those if these uids change the order
        // only get status == 0
        List<Guid> indexed = uids.ToList();
        using var db = await DbConnector.GetDb();
        var sorted = await db.Db.FetchAsync<LibraryFile>($"select * from {Wrap(nameof(LibraryFile))} where {Wrap(nameof(LibraryFile.Status))} = 0 and ( {Wrap("ProcessingOrder")} > 0 or  {Wrap(nameof(LibraryFile.Uid))} in ({strUids}))");
        sorted = sorted.OrderBy(x =>
        {
            int index = indexed.IndexOf(x.Uid);
            if (index < 0)
                return 10000 + x.Order;
            return index;
        }).ToList();

        var commands = new List<string>();
        for(int i=0;i<sorted.Count;i++)
        {
            var file = sorted[i];
            file.Order = i + 1;
            commands.Add($"update {Wrap(nameof(LibraryFile))}  set {Wrap("ProcessingOrder")} = {file.Order} where {Wrap(nameof(LibraryFile.Uid))} = '{file.Uid}';");
        }

        await db.Db.ExecuteAsync(string.Join("\n", commands));
    }

    /// <summary>
    /// Updates a moved file in the database
    /// </summary>
    /// <param name="file">the file to update</param>
    /// <returns>true if any files were updated</returns>
    public async Task<bool> UpdateMovedFile(LibraryFile file)
    {
        string sql = $"update {Wrap(nameof(LibraryFile))} set {Wrap(nameof(LibraryFile.Name))} = @0, " +
                     $" {Wrap(nameof(LibraryFile.RelativePath))} = @1, " +
                     $" {Wrap(nameof(LibraryFile.OutputPath))} = @2, " +
                     $" {Wrap(nameof(LibraryFile.CreationTime))} = {DbConnector.FormatDateQuoted(file.CreationTime)}, " +
                     $" {Wrap(nameof(LibraryFile.LastWriteTime))} = {DbConnector.FormatDateQuoted(file.LastWriteTime)} " +
                     $" where {Wrap(nameof(LibraryFile.Uid))} = '{file.Uid}'";
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteAsync(sql, file.Name, file.RelativePath, file.OutputPath) > 0;
    }
    
    /// <summary>
    /// Gets a list of all filenames and the file creation times
    /// </summary>
    /// <param name="libraryUid">the UID of the library</param>
    /// <returns>a list of all filenames</returns>
    public async Task<List<KnownFileInfo>> GetKnownLibraryFilesWithCreationTimes(Guid libraryUid)
    {
        using var db = await DbConnector.GetDb();
        var list = await db.Db.FetchAsync<KnownFileInfo>(
            $"select {Wrap(nameof(KnownFileInfo.Name))},{Wrap(nameof(KnownFileInfo.Status))}," +
            $" {Wrap(nameof(KnownFileInfo.CreationTime))},{Wrap(nameof(KnownFileInfo.LastWriteTime))}" +
            $" from {Wrap(nameof(LibraryFile))} " +
            $" where {Wrap(nameof(LibraryFile.LibraryUid))} = '{libraryUid}'");
        return list;
    }

    /// <summary>
    /// Gets the total storage saved
    /// </summary>
    /// <returns>the total storage saved</returns>
    public async Task<long> GetTotalStorageSaved()
    {
        string sql = $@"SELECT SUM(
    CASE 
        WHEN {Wrap(nameof(LibraryFile.Status))} = {(int)FileStatus.Processed} AND {Wrap(nameof(LibraryFile.FinalSize))} < {Wrap(nameof(LibraryFile.OriginalSize))} 
            THEN {Wrap(nameof(LibraryFile.OriginalSize))} - {Wrap(nameof(LibraryFile.FinalSize))} 
        ELSE 0 
    END
) 
FROM {Wrap(nameof(LibraryFile))}";
        
        using var db = await DbConnector.GetDb();
        return await db.Db.ExecuteScalarAsync<long>(sql);
    }
    
    /// <summary>
    /// Gets the total rows, sum of OriginalSize, and sum of FinalSize from the LibraryFile table grouped by Library.
    /// </summary>
    /// <returns>A list of library statistics</returns>
    public async Task<List<(Guid LibraryUid, int TotalFiles, long SumOriginalSize, long SumFinalSize)>> GetLibraryFileStats()
    {
        string sql = $@"
        SELECT 
            {Wrap(nameof(LibraryFile.LibraryUid))} AS {Wrap("LibraryUid")},
            COUNT(*) AS {Wrap("TotalFiles")}, 
            SUM({Wrap(nameof(LibraryFile.OriginalSize))}) AS {Wrap("SumOriginalSize")}, 
            SUM({Wrap(nameof(LibraryFile.FinalSize))}) AS {Wrap("SumFinalSize")} 
        FROM {Wrap(nameof(LibraryFile))}
        GROUP BY {Wrap(nameof(LibraryFile.LibraryUid))}";
    
        using var db = await DbConnector.GetDb();
        var results = await db.Db.FetchAsync<(Guid LibraryUid, int TotalFiles, long SumOriginalSize, long SumFinalSize)>(sql);

        return results;
    }

    /// <summary>
    /// Performs a search for files
    /// </summary>
    /// <param name="filter">the search filter</param>
    /// <returns>the matching files</returns>
    public async Task<List<LibraryFile>> Search(LibraryFileSearchModel filter)
    {
        List<string> wheres = new();

        wheres.Add(Wrap(nameof(LibraryFile.CreationTime)) + " between " +
                   DbConnector.FormatDateQuoted(filter.FromDate.Year > 1970 ? filter.FromDate : new DateTime(1970,1,1)) + " and " +
                   DbConnector.FormatDateQuoted(filter.ToDate.Year < 2200 ? filter.ToDate : new DateTime(2200,12, 31)));

        if (filter.Status != null)
        {
            wheres.Add(Wrap(nameof(LibraryFile.Status)) + " = " + ((int)filter.Status.Value));
        }

        if (string.IsNullOrWhiteSpace(filter.LibraryName) == false)
        {
            wheres.Add("lower(" + Wrap(nameof(LibraryFile.LibraryName)) + ") " +
                       $"like lower('%{filter.LibraryName.Replace("'", "''").Replace(" ", "%")}%')");
        }
        

        if (string.IsNullOrWhiteSpace(filter.Path) == false)
        {
            wheres.Add("( lower(" + Wrap(nameof(LibraryFile.Name)) + ") " +
                       $"like lower('%{filter.Path.Replace("'", "''").Replace(" ", "%")}%')" +
                       " or lower(" + Wrap(nameof(LibraryFile.OutputPath)) + ")" +
                       $"like lower('%{filter.Path.Replace("'", "''").Replace(" ", "%")}%')" +
                       ")");
        }

        string sql = "select * from " + Wrap(nameof(LibraryFile)) + " " +
                     "where " + string.Join(" and ", wheres);
        
        sql += DbType switch
        {
            DatabaseType.SqlServer => $" OFFSET 0 ROWS FETCH NEXT {filter.Limit} ROWS ONLY",
            _ => $" LIMIT {filter.Limit}",
        };
        
        using var db = await DbConnector.GetDb();
        return await db.Db.FetchAsync<LibraryFile>(sql);
    }


    /// <summary>
    /// Gets the total files each node has processed
    /// </summary>
    /// <returns>A dictionary of the total files indexed by the node UID</returns>
    public async Task<Dictionary<Guid, int>> GetNodeTotalFiles()
    {
        string sql = $@"SELECT {Wrap(nameof(LibraryFile.NodeUid))}, COUNT(*) AS {Wrap("FileCount")}
FROM {Wrap(nameof(LibraryFile))} GROUP BY {Wrap(nameof(LibraryFile.NodeUid))};";
            
        using var db = await DbConnector.GetDb();
        var result =  await db.Db.FetchAsync<(Guid NodeUid, int FileCount)>(sql);
        return result.Where(x => x.NodeUid != Guid.Empty).DistinctBy(x => x.NodeUid)
            .ToDictionary(x => x.NodeUid, x => x.FileCount);
    }

    
    /// <summary>
    /// Gets if a file exists
    /// </summary>
    /// <param name="name">the name of the file</param>
    /// <returns>true if exists, otherwise false</returns>
    public async Task<bool> FileExists(string name)
    {
        string sql =
            $"select {Wrap(nameof(LibraryFile.Uid))} from {Wrap(nameof(LibraryFile))} where {Wrap(nameof(LibraryFile.Name))} = @0";
        
        using var db = await DbConnector.GetDb();
        return db.Db.ExecuteScalar<Guid?>(sql, name) != null;
    }

    /// <summary>
    /// Reset processing for the files
    /// </summary>
    /// <param name="model">the reprocess model</param>
    /// <param name="onlySetProcessInfo">if only the process information should be set, ie these are unprocessed files</param>
    public async Task Reprocess(ReprocessModel model, bool onlySetProcessInfo = false)
    {
        foreach (var uid in model.Uids)
        {
            var file = await Get(uid);
            if (file == null)
                continue;

            if (onlySetProcessInfo == false)
            {
                file.DateModified = DateTime.UtcNow;
                file.Status = FileStatus.Unprocessed;

                if (model.BottomOfQueue)
                    file.DateCreated = DateTime.UtcNow;
            }

            if (model.Mode == ReprocessModel.CustomVariablesMode.Replace)
                file.CustomVariables = model.CustomVariables ?? new ();
            else if (model.Mode == ReprocessModel.CustomVariablesMode.Merge && model.CustomVariables?.Any() == true)
            {
                file.CustomVariables ??= new();
                foreach (var kv in model.CustomVariables)
                {
                    file.CustomVariables[kv.Key] = kv.Value;
                }
            }

            if (model.Flow != null)
            {
                file.FlowUid = model.Flow.Uid;
                file.FlowName = model.Flow.Name;
            }
            else if(onlySetProcessInfo == false)
            {
                file.FlowUid = null;
                file.FlowName = string.Empty;
            }
            if (model.Node != null)
            {
                file.NodeUid = model.Node.Uid;
                file.NodeName = model.Node.Name;
            }
            else if (onlySetProcessInfo == false)
            {
                file.NodeUid = null;
                file.NodeName = string.Empty;
            }

            await Update(file);
        }
    }
}