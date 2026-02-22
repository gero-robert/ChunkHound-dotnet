using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Python.Runtime;

namespace ChunkHound.Providers;

/// <summary>
/// Service class for LanceDB operations using pythonnet integration.
/// Provides a thin C# wrapper around Python LanceDB SDK for vector database operations.
/// </summary>
public class LanceDbService : IDisposable
{
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly ILogger? _logger;
    private dynamic? _db;
    private dynamic? _chunksTable;
    private dynamic? _filesTable;
    private bool _isInitialized;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the LanceDbService.
    /// </summary>
    /// <param name="dbPath">Path to the LanceDB database directory.</param>
    /// <param name="logger">Optional logger instance.</param>
    public LanceDbService(string dbPath, ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(dbPath))
            throw new ArgumentNullException(nameof(dbPath));

        _logger = logger;

        InitializePythonEngine();
        InitializeDatabase(dbPath);
    }

    /// <summary>
    /// Initializes the Python runtime engine.
    /// </summary>
    private void InitializePythonEngine()
    {
        try
        {
            if (!PythonEngine.IsInitialized)
            {
                // Configure Python environment before initialization
                ConfigurePythonEnvironment();

                PythonEngine.Initialize();
                _logger?.LogInformation("Python engine initialized");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Python engine");
            throw;
        }
    }

    /// <summary>
    /// Configures Python environment for virtual environment support.
    /// </summary>
    private static void ConfigurePythonEnvironment()
    {
        // Use the base uv Python installation for PYTHONHOME
        var pythonHome = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "uv", "python", "cpython-3.12.12-windows-x86_64-none");

        // Add venv site-packages to PYTHONPATH
        var venvPath = @"e:\dev\github\chunkhound-dotnet\python-deps\.venv";
        var pythonPath = Path.Combine(venvPath, "Lib", "site-packages");

        // Set environment variables for pythonnet
        Environment.SetEnvironmentVariable("PYTHONHOME", pythonHome);
        Environment.SetEnvironmentVariable("PYTHONPATH", pythonPath);

        // Set the Python DLL path for pythonnet (must be set before PythonEngine.Initialize)
        var pythonDllPath = Path.Combine(pythonHome, "python312.dll");

        if (System.IO.File.Exists(pythonDllPath))
        {
            Runtime.PythonDLL = pythonDllPath;
        }
    }

    /// <summary>
    /// Initializes the LanceDB database connection and tables.
    /// </summary>
    /// <param name="dbPath">Path to the database.</param>
    private void InitializeDatabase(string dbPath)
    {
        var threadId = Thread.CurrentThread.ManagedThreadId;
        _logger?.LogDebug("InitializeDatabase dbPath={DbPath} Thread={ThreadId}: Acquiring GIL", dbPath, threadId);
        using (Py.GIL())
        {
            _logger?.LogDebug("InitializeDatabase dbPath={DbPath} Thread={ThreadId}: Acquired GIL", dbPath, threadId);
            try
            {
                // Ensure the venv site-packages is in sys.path
                var venvPath = @"e:\dev\github\chunkhound-dotnet\python-deps\.venv";
                var pythonPath = Path.Combine(venvPath, "Lib", "site-packages");

                dynamic sys = Py.Import("sys");
                sys.path.insert(0, pythonPath.ToString());
                _logger?.LogDebug("Added {PythonPath} to sys.path", pythonPath);

                dynamic lancedb = Py.Import("lancedb");
                _db = lancedb.connect(dbPath);

                // Open or create tables
                _chunksTable = GetOrCreateTable("chunks");
                _filesTable = GetOrCreateTable("files");

                _isInitialized = true;
                _logger?.LogInformation("LanceDB database initialized at {DbPath}", dbPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "InitializeDatabase dbPath={DbPath} Thread={ThreadId}: Failed to initialize LanceDB database", dbPath, threadId);
                throw;
            }
        }
        _logger?.LogDebug("InitializeDatabase dbPath={DbPath} Thread={ThreadId}: GIL released", dbPath, threadId);
    }

    /// <summary>
    /// Gets or creates a table in the database.
    /// </summary>
    /// <param name="tableName">Name of the table.</param>
    /// <returns>The table object.</returns>
    private dynamic GetOrCreateTable(string tableName)
    {
        try
        {
            return _db.open_table(tableName);
        }
        catch
        {
            // Table doesn't exist, will be created when needed
            _logger?.LogDebug("Table {TableName} does not exist, will be created on first use", tableName);
            return null;
        }
    }

    /// <summary>
    /// Performs vector similarity search on the chunks table.
    /// </summary>
    /// <param name="vector">The query embedding vector.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="filter">Optional filter string.</param>
    /// <returns>List of search results with metadata.</returns>
    public List<Dictionary<string, object>> Search(float[] vector, int limit = 10, string? filter = null)
    {
        if (!_isInitialized || _chunksTable == null)
            throw new InvalidOperationException("LanceDB service not initialized");

        var threadId = Thread.CurrentThread.ManagedThreadId;
        _logger?.LogDebug("Search Thread={ThreadId}: Acquiring GIL", threadId);
        using (Py.GIL())
        {
            _logger?.LogDebug("Search Thread={ThreadId}: Acquired GIL", threadId);
            try
            {
                // Convert C# array to Python list
                var pyVector = new PyList();
                foreach (var v in vector)
                    pyVector.Append(new PyFloat(v));

                // Build search query
                dynamic query = _chunksTable.search(pyVector).limit(limit);

                if (!string.IsNullOrEmpty(filter))
                    query = query.where(filter);

                // Execute search
                dynamic results = query.to_list();

                // Convert results to C# dictionaries
                var searchResults = new List<Dictionary<string, object>>();
                foreach (var result in results)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var key in result.keys())
                    {
                        var keyStr = key.ToString();
                        var value = result[keyStr];
                        dict[keyStr] = ConvertPythonValue(value);
                    }
                    searchResults.Add(dict);
                }

                _logger?.LogDebug("Search Thread={ThreadId}: Vector search completed, found {Count} results", threadId, searchResults.Count);
                return searchResults;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Search Thread={ThreadId}: Vector search failed", threadId);
                throw;
            }
        }
        _logger?.LogDebug("Search Thread={ThreadId}: GIL released", threadId);
    }

    /// <summary>
    /// Adds a batch of records to the specified table.
    /// </summary>
    /// <param name="tableName">Name of the table.</param>
    /// <param name="records">Records to add.</param>
    public void AddBatch(string tableName, IEnumerable<Dictionary<string, object>> records)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("LanceDB service not initialized");

        var threadId = Thread.CurrentThread.ManagedThreadId;
        _logger?.LogDebug("AddBatch table={TableName} Thread={ThreadId}: Waiting for write semaphore", tableName, threadId);
        _writeSemaphore.Wait();
        _logger?.LogDebug("AddBatch table={TableName} Thread={ThreadId}: Acquired write semaphore", tableName, threadId);
        try
        {
            _logger?.LogDebug("AddBatch table={TableName} Thread={ThreadId}: Acquiring GIL", tableName, threadId);
            using (Py.GIL())
            {
                _logger?.LogDebug("AddBatch table={TableName} Thread={ThreadId}: Acquired GIL", tableName, threadId);
                dynamic table = tableName == "chunks" ? _chunksTable : _filesTable;

                if (table == null)
                {
                    // Create table if it doesn't exist
                    table = CreateTable(tableName, records);
                    if (tableName == "chunks") _chunksTable = table;
                    else _filesTable = table;
                }

                // Convert records to Python list of dicts
                var pyRecords = new PyList();
                foreach (var record in records)
                {
                    var pyDict = new PyDict();
                    foreach (var kvp in record)
                    {
                        pyDict[kvp.Key] = ConvertToPythonValue(kvp.Value);
                    }
                    pyRecords.Append(pyDict);
                }

                // Add records
                table.add(pyRecords);
                _logger?.LogDebug("AddBatch table={TableName} Thread={ThreadId}: Added {Count} records", tableName, threadId, pyRecords.Length());
            }
            _logger?.LogDebug("AddBatch table={TableName} Thread={ThreadId}: GIL released", tableName, threadId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AddBatch table={TableName} Thread={ThreadId}: Failed to add batch", tableName, threadId);
            throw;
        }
        finally
        {
            _logger?.LogDebug("AddBatch table={TableName} Thread={ThreadId}: Releasing write semaphore", tableName, threadId);
            _writeSemaphore.Release();
            _logger?.LogDebug("AddBatch table={TableName} Thread={ThreadId}: Write semaphore released", tableName, threadId);
        }
    }

    /// <summary>
    /// Creates a table with inferred schema from records.
    /// </summary>
    /// <param name="tableName">Name of the table.</param>
    /// <param name="records">Sample records to infer schema.</param>
    /// <returns>The created table.</returns>
    private dynamic CreateTable(string tableName, IEnumerable<Dictionary<string, object>> records)
    {
        // For simplicity, create table without explicit schema
        // LanceDB can infer schema from data
        var sampleRecord = records.FirstOrDefault();
        if (sampleRecord == null)
            throw new ArgumentException("Cannot create table without sample data");

        var pyDict = new PyDict();
        foreach (var kvp in sampleRecord)
        {
            pyDict[kvp.Key] = ConvertToPythonValue(kvp.Value);
        }

        var pyList = new PyList();
        pyList.Append(pyDict);
        return _db.create_table(tableName, pyList);
    }

    /// <summary>
    /// Converts Python value to C# object.
    /// </summary>
    private static object ConvertPythonValue(dynamic value)
    {
        if (value is PyFloat) return float.Parse(value.ToString());
        if (value is PyInt) return long.Parse(value.ToString());
        if (value is PyString pyString) return pyString.ToString();
        if (value is PyList pyList)
        {
            var list = new List<object>();
            foreach (var item in pyList)
                list.Add(ConvertPythonValue(item));
            return list;
        }
        if (value is PyDict pyDict)
        {
            var dict = new Dictionary<string, object>();
            foreach (var key in pyDict.Keys())
            {
                var keyStr = key.ToString();
                dict[keyStr] = ConvertPythonValue(pyDict[keyStr]);
            }
            return dict;
        }
        return value;
    }

    /// <summary>
    /// Converts C# value to Python object.
    /// </summary>
    private static dynamic ConvertToPythonValue(object value)
    {
        return value switch
        {
            float f => new PyFloat(f),
            int i => new PyInt(i),
            long l => new PyInt(l),
            string s => new PyString(s),
            DateTime dt => ((dynamic)Py.Import("datetime")).datetime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond * 1000),
            List<float> list => new PyList(list.Select(x => (PyObject)new PyFloat(x)).ToArray()),
            _ => value
        };
    }

    public List<Dictionary<string, object>> Query(string tableName, string? filter = null, int limit = -1)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("LanceDB service not initialized");

        var threadId = Thread.CurrentThread.ManagedThreadId;
        _logger?.LogDebug("Query table={TableName} Thread={ThreadId}: Acquiring GIL", tableName, threadId);
        var searchResults = new List<Dictionary<string, object>>();
        using (Py.GIL())
        {
            _logger?.LogDebug("Query table={TableName} Thread={ThreadId}: Acquired GIL", tableName, threadId);
            try
            {
                dynamic table = tableName == "chunks" ? _chunksTable : _filesTable;
                if (table == null)
                    return new List<Dictionary<string, object>>();

                dynamic query = table.search();

                if (!string.IsNullOrEmpty(filter))
                {
                    query = query.where(filter);
                }

                if (limit > 0)
                {
                    query = query.limit(limit);
                }

                dynamic results = query.to_list();

                // Convert results to C# dictionaries (unchanged)
                foreach (var result in results)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var key in result.keys())
                    {
                        var keyStr = key.ToString();
                        var value = result[keyStr];
                        dict[keyStr] = ConvertPythonValue(value);
                    }
                    searchResults.Add(dict);
                }

                _logger?.LogDebug("Query table={TableName} Thread={ThreadId}: completed, found {Count} results", tableName, threadId, searchResults.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Query table={TableName} Thread={ThreadId}: failed", tableName, threadId);
                throw;
            }
        }
        _logger?.LogDebug("Query table={TableName} Thread={ThreadId}: GIL released", tableName, threadId);
        
        return searchResults;
    }

    /// <summary>
    /// Updates embeddings for existing chunks.
    /// </summary>
    /// <param name="embeddingsData">The embedding data to update.</param>
    public void UpdateEmbeddings(IEnumerable<Dictionary<string, object>> embeddingsData)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("LanceDB service not initialized");

        var threadId = Thread.CurrentThread.ManagedThreadId;
        _logger?.LogDebug("UpdateEmbeddings Thread={ThreadId}: Waiting for write semaphore", threadId);
        _writeSemaphore.Wait();
        _logger?.LogDebug("UpdateEmbeddings Thread={ThreadId}: Acquired write semaphore", threadId);
        try
        {
            _logger?.LogDebug("UpdateEmbeddings Thread={ThreadId}: Acquiring GIL", threadId);
            using (Py.GIL())
            {
                _logger?.LogDebug("UpdateEmbeddings Thread={ThreadId}: Acquired GIL", threadId);
                if (_chunksTable == null)
                {
                    // Create table if it doesn't exist
                    _chunksTable = CreateTable("chunks", embeddingsData);
                }

                // Convert to Python list of dicts
                var pyRecords = new PyList();
                foreach (var record in embeddingsData)
                {
                    var pyDict = new PyDict();
                    foreach (var kvp in record)
                    {
                        pyDict[kvp.Key] = ConvertToPythonValue(kvp.Value);
                    }
                    pyRecords.Append(pyDict);
                }

                // Use merge to update existing records
                _chunksTable.merge_insert("id").when_matched_update_all().when_not_matched_insert_all().execute(pyRecords);

                _logger?.LogDebug("UpdateEmbeddings Thread={ThreadId}: Updated embeddings for {Count} chunks", threadId, pyRecords.Length());
            }
            _logger?.LogDebug("UpdateEmbeddings Thread={ThreadId}: GIL released", threadId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "UpdateEmbeddings Thread={ThreadId}: Failed to update embeddings", threadId);
            throw;
        }
        finally
        {
            _logger?.LogDebug("UpdateEmbeddings Thread={ThreadId}: Releasing write semaphore", threadId);
            _writeSemaphore.Release();
            _logger?.LogDebug("UpdateEmbeddings Thread={ThreadId}: Write semaphore released", threadId);
        }
    }

    /// <summary>
    /// Optimizes the database tables.
    /// </summary>
    public void Optimize()
    {
        if (!_isInitialized)
            return;

        var threadId = Thread.CurrentThread.ManagedThreadId;
        _logger?.LogDebug("Optimize Thread={ThreadId}: Waiting for write semaphore", threadId);
        _writeSemaphore.Wait();
        _logger?.LogDebug("Optimize Thread={ThreadId}: Acquired write semaphore", threadId);
        try
        {
            _logger?.LogDebug("Optimize Thread={ThreadId}: Acquiring GIL", threadId);
            using (Py.GIL())
            {
                _logger?.LogDebug("Optimize Thread={ThreadId}: Acquired GIL", threadId);
                if (_chunksTable != null)
                    _chunksTable.optimize();

                if (_filesTable != null)
                    _filesTable.optimize();

                _logger?.LogInformation("Database optimization completed");
            }
            _logger?.LogDebug("Optimize Thread={ThreadId}: GIL released", threadId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Optimize Thread={ThreadId}: Database optimization failed", threadId);
            throw;
        }
        finally
        {
            _logger?.LogDebug("Optimize Thread={ThreadId}: Releasing write semaphore", threadId);
            _writeSemaphore.Release();
            _logger?.LogDebug("Optimize Thread={ThreadId}: Write semaphore released", threadId);
        }
    }

    /// <summary>
    /// Gets statistics for a table.
    /// </summary>
    /// <param name="tableName">Name of the table.</param>
    /// <returns>Table statistics.</returns>
    public Dictionary<string, object> GetTableStats(string tableName)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("LanceDB service not initialized");

        var threadId = Thread.CurrentThread.ManagedThreadId;
        _logger?.LogDebug("GetTableStats table={TableName} Thread={ThreadId}: Acquiring GIL", tableName, threadId);
        using (Py.GIL())
        {
            _logger?.LogDebug("GetTableStats table={TableName} Thread={ThreadId}: Acquired GIL", tableName, threadId);
            try
            {
                dynamic table = tableName == "chunks" ? _chunksTable : _filesTable;
                if (table == null)
                    return new Dictionary<string, object>();

                var stats = table.stats();
                var result = new Dictionary<string, object>();

                // Extract relevant stats
                foreach (var key in stats.keys())
                {
                    var keyStr = key.ToString();
                    result[keyStr] = ConvertPythonValue(stats[keyStr]);
                }

                _logger?.LogDebug("GetTableStats table={TableName} Thread={ThreadId}: Retrieved {Count} stats", tableName, threadId, result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetTableStats table={TableName} Thread={ThreadId}: Failed to get table stats", tableName, threadId);
                return new Dictionary<string, object>();
            }
        }
        _logger?.LogDebug("GetTableStats table={TableName} Thread={ThreadId}: GIL released", tableName, threadId);
    }

    /// <summary>
    /// Clears all data by dropping and recreating the chunks and files tables.
    /// </summary>
    public void ClearAllData()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("LanceDB service not initialized");

        var threadId = Thread.CurrentThread.ManagedThreadId;
        _logger?.LogDebug("ClearAllData: Thread {ThreadId}: Waiting for write semaphore", threadId);
        _writeSemaphore.Wait();
        _logger?.LogDebug("ClearAllData: Thread {ThreadId}: Acquired write semaphore", threadId);
        try
        {
            _logger?.LogDebug("ClearAllData: Thread {ThreadId}: Acquiring GIL", threadId);
            using (Py.GIL())
            {
                _logger?.LogDebug("ClearAllData: Thread {ThreadId}: Acquired GIL", threadId);
                _logger?.LogDebug("ClearAllData: Thread {ThreadId}: Starting table operations", threadId);
                // Delete all data from existing tables instead of dropping
                try
                {
                    var chunksTable = _db.open_table("chunks");
                    chunksTable.delete("id >= 0");
                    _logger?.LogDebug("ClearAllData: Thread {ThreadId}: Deleted all data from chunks table", threadId);
                }
                catch
                {
                    _logger?.LogDebug("ClearAllData: Thread {ThreadId}: Chunks table doesn't exist", threadId);
                }

                try
                {
                    var filesTable = _db.open_table("files");
                    filesTable.delete("id >= 0");
                    _logger?.LogDebug("ClearAllData: Thread {ThreadId}: Deleted all data from files table", threadId);
                }
                catch
                {
                    _logger?.LogDebug("ClearAllData: Thread {ThreadId}: Files table doesn't exist", threadId);
                }

                // Keep table references, just clear data

                _logger?.LogInformation("Cleared all data (rows deleted)");
            }
            _logger?.LogDebug("ClearAllData: Thread {ThreadId}: GIL released", threadId);
        }
        finally
        {
            _logger?.LogDebug("ClearAllData: Thread {ThreadId}: Releasing write semaphore", threadId);
            _writeSemaphore.Release();
            _logger?.LogDebug("ClearAllData: Thread {ThreadId}: Write semaphore released", threadId);
        }
    }

    /// <summary>
    /// Disposes the service and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        var threadId = Thread.CurrentThread.ManagedThreadId;
        // Close the database connection to release GIL
        if (_db != null)
        {
            _logger?.LogDebug("Dispose Thread={ThreadId}: Acquiring GIL for db.close()", threadId);
            try
            {
                using (Py.GIL())
                {
                    _logger?.LogDebug("Dispose Thread={ThreadId}: Acquired GIL for db.close()", threadId);
                    _db.close();
                }
                _logger?.LogDebug("Dispose Thread={ThreadId}: GIL released after db.close()", threadId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Dispose Thread={ThreadId}: Failed to close LanceDB connection", threadId);
            }
        }

        _logger?.LogDebug("Dispose Thread={ThreadId}: Disposing write semaphore", threadId);
        _writeSemaphore.Dispose();

        // Clear references
        _db = null;
        _chunksTable = null;
        _filesTable = null;

        if (PythonEngine.IsInitialized)
        {
            // Note: PythonEngine.Shutdown() is not typically called in production
            // as it may affect other parts of the application using Python
        }

        _isDisposed = true;
        _logger?.LogInformation("LanceDbService disposed");
    }
}