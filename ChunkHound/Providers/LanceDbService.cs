using System;
using System.Collections.Generic;
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
    private readonly ILogger<LanceDbService>? _logger;
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
    public LanceDbService(string dbPath, ILogger<LanceDbService>? logger = null)
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
    /// Initializes the LanceDB database connection and tables.
    /// </summary>
    /// <param name="dbPath">Path to the database.</param>
    private void InitializeDatabase(string dbPath)
    {
        using (Py.GIL())
        {
            try
            {
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
                _logger?.LogError(ex, "Failed to initialize LanceDB database");
                throw;
            }
        }
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
    public async Task<List<Dictionary<string, object>>> SearchAsync(float[] vector, int limit = 10, string? filter = null)
    {
        if (!_isInitialized || _chunksTable == null)
            throw new InvalidOperationException("LanceDB service not initialized");

        using (Py.GIL())
        {
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

                _logger?.LogDebug("Vector search completed, found {Count} results", searchResults.Count);
                return searchResults;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Vector search failed");
                throw;
            }
        }
    }

    /// <summary>
    /// Adds a batch of records to the specified table.
    /// </summary>
    /// <param name="tableName">Name of the table.</param>
    /// <param name="records">Records to add.</param>
    public async Task AddBatchAsync(string tableName, IEnumerable<Dictionary<string, object>> records)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("LanceDB service not initialized");

        await _writeSemaphore.WaitAsync();
        try
        {
            using (Py.GIL())
            {
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
                _logger?.LogDebug("Added {Count} records to {TableName} table", pyRecords.Length(), tableName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add batch to {TableName}", tableName);
            throw;
        }
        finally
        {
            _writeSemaphore.Release();
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
            List<float> list => new PyList(list.Select(x => (PyObject)new PyFloat(x)).ToArray()),
            _ => value
        };
    }

    /// <summary>
    /// Queries records from the specified table using a filter.
    /// </summary>
    /// <param name="tableName">Name of the table.</param>
    /// <param name="filter">Filter string.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <returns>List of matching records.</returns>
    public async Task<List<Dictionary<string, object>>> QueryAsync(string tableName, string? filter = null, int limit = -1)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("LanceDB service not initialized");

        using (Py.GIL())
        {
            try
            {
                dynamic table = tableName == "chunks" ? _chunksTable : _filesTable;
                if (table == null)
                    return new List<Dictionary<string, object>>();

                dynamic query = table;
                if (!string.IsNullOrEmpty(filter))
                    query = query.where(filter);

                if (limit > 0)
                    query = query.limit(limit);

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

                _logger?.LogDebug("Query completed, found {Count} results", searchResults.Count);
                return searchResults;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Query failed");
                throw;
            }
        }
    }

    /// <summary>
    /// Updates embeddings for existing chunks.
    /// </summary>
    /// <param name="embeddingsData">The embedding data to update.</param>
    public async Task UpdateEmbeddingsAsync(IEnumerable<Dictionary<string, object>> embeddingsData)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("LanceDB service not initialized");

        await _writeSemaphore.WaitAsync();
        try
        {
            using (Py.GIL())
            {
                if (_chunksTable == null)
                    throw new InvalidOperationException("Chunks table not initialized");

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

                _logger?.LogDebug("Updated embeddings for {Count} chunks", pyRecords.Length());
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update embeddings");
            throw;
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    /// <summary>
    /// Optimizes the database tables.
    /// </summary>
    public async Task OptimizeAsync()
    {
        if (!_isInitialized)
            return;

        await _writeSemaphore.WaitAsync();
        try
        {
            using (Py.GIL())
            {
                if (_chunksTable != null)
                    _chunksTable.optimize();

                if (_filesTable != null)
                    _filesTable.optimize();

                _logger?.LogInformation("Database optimization completed");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Database optimization failed");
            throw;
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets statistics for a table.
    /// </summary>
    /// <param name="tableName">Name of the table.</param>
    /// <returns>Table statistics.</returns>
    public async Task<Dictionary<string, object>> GetTableStatsAsync(string tableName)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("LanceDB service not initialized");

        using (Py.GIL())
        {
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

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get table stats for {TableName}", tableName);
                return new Dictionary<string, object>();
            }
        }
    }

    /// <summary>
    /// Disposes the service and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _writeSemaphore.Dispose();

        if (PythonEngine.IsInitialized)
        {
            // Note: PythonEngine.Shutdown() is not typically called in production
            // as it may affect other parts of the application using Python
        }

        _isDisposed = true;
        _logger?.LogInformation("LanceDbService disposed");
    }
}