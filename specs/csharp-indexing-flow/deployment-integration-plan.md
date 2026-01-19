# C# Indexing Flow Deployment and Integration Plan

> **Spec Version:** 1.0 | **Status:** final

## Overview

This document outlines the deployment and integration strategy for the C# implementation of ChunkHound's indexing flow. The C# version provides a high-performance alternative to the Python implementation, targeting .NET environments with improved concurrency and lower memory overhead.

## Validation Summary

### Alignment with Original Spec

✅ **Metrics:** All target metrics match (indexing <5min for 10k files, >95% embedding reuse, <10 fragments)

✅ **Constraints:** Batch thresholds and DB locking implemented with C# equivalents (ReaderWriterLockSlim)

✅ **Flows:** Pipeline architecture matches with async adaptations (EmbedAsync, InsertChunksBatchAsync)

✅ **Modules:** All core modules ported with proper C# async patterns and interfaces

✅ **Boundaries:** Service boundaries maintained with interface-based contracts

### Final Adjustments Recommended

1. **Cancellation Support:** Add `CancellationToken` parameters to all async methods for graceful shutdown
2. **Error Handling:** Implement consistent `AggregateException` handling in parallel operations
3. **Logging Enhancement:** Add structured logging with configurable levels (Debug/Info/Warn/Error)
4. **Health Checks:** Add pipeline health monitoring endpoints
5. **Configuration:** Add runtime configuration for thread pool sizes and queue capacities
6. **Performance:** Add C#-specific benchmarks against Python baseline

## Deployment Strategy

### Build Process

```bash
# 1. Restore dependencies
dotnet restore ChunkHound.sln

# 2. Build release
dotnet build --configuration Release ChunkHound.sln

# 3. Run tests
dotnet test --configuration Release ChunkHound.sln

# 4. Create packages
dotnet pack --configuration Release ChunkHound.sln
```

### Packaging

- **CLI Tool:** Single executable via `dotnet publish -r win-x64 --self-contained`
- **NuGet Packages:** Separate packages for Core, Services, Providers
- **Docker:** Multi-stage build with .NET runtime image

### Distribution

| Target | Method | Path |
|--------|--------|------|
| Windows | MSI installer | `dist/chunkhound-X.Y.Z.msi` |
| Linux | DEB/RPM packages | `dist/chunkhound-X.Y.Z.deb` |
| Docker | Image | `chunkhound/chunkhound-csharp:X.Y.Z` |
| NuGet | Library packages | `nuget.org/package/ChunkHound.Core` |

## Integration Strategy

### Database Compatibility

- **Schema:** 100% compatible with Python LanceDB schema
- **Migration:** Automatic schema detection and migration on startup
- **Concurrent Access:** ReaderWriterLockSlim allows concurrent reads during indexing

### Configuration Sharing

```json
{
  "embedding": {
    "provider": "openai",
    "model": "text-embedding-3-small",
    "batch_size": 100
  },
  "database": {
    "type": "lancedb",
    "path": "./chunkhound.db"
  },
  "indexing": {
    "max_workers": 8,
    "queue_capacity": 1000
  }
}
```

### CLI Compatibility

```bash
# Python (existing)
chunkhound index /path/to/code --config config.json

# C# (new)
ChunkHound.Cli.exe index /path/to/code --config config.json
```

### MCP Integration

- **Stdio Server:** `ChunkHound.Mcp.Stdio.dll` for local MCP clients
- **HTTP Server:** `ChunkHound.Mcp.Http.dll` for remote MCP access
- **Protocol:** Full MCP 2024-11-05 compatibility

## Testing Strategy

### Unit Testing
- xUnit for all modules
- Moq for interface mocking
- FluentAssertions for assertions

### Integration Testing
- TestContainers for database isolation
- Fake embedding provider for cost-free testing
- Performance benchmarks vs Python baseline

### E2E Testing
```csharp
[Fact]
public async Task FullIndexingFlow_10kFiles_CompletesUnder5Minutes()
{
    // Arrange
    var config = new IndexingConfig { /* ... */ };

    // Act
    var result = await _coordinator.ProcessDirectoryAsync("/test/data", config);

    // Assert
    Assert.True(result.Duration < TimeSpan.FromMinutes(5));
    Assert.True(result.EmbeddingReuseRate > 0.95);
}
```

## Migration Path

### Phase 1: Side-by-Side (Week 1-2)
- Deploy C# version alongside Python
- Use different database paths for isolation
- Compare performance metrics

### Phase 2: Feature Parity (Week 3-4)
- Implement all Python features in C#
- Cross-validate results between implementations
- Update documentation

### Phase 3: Production Cutover (Week 5)
- Switch primary indexer to C# version
- Keep Python as fallback
- Monitor for regressions

## Performance Expectations

| Metric | Python Baseline | C# Target | Expected Improvement |
|--------|----------------|-----------|---------------------|
| Memory Usage | 2-4GB | 1-2GB | 50% reduction |
| CPU Utilization | 70-90% | 60-80% | 15% improvement |
| Startup Time | 5-10s | 2-5s | 50% faster |
| Throughput | 100-150 files/min | 150-200 files/min | 30% increase |

## Monitoring and Observables

### Metrics
- Queue depths per stage
- Batch sizes and API call frequencies
- Error rates by component
- Memory/CPU usage trends

### Logging
```csharp
_logger.LogInformation("Indexing completed: {FilesProcessed} files, {ChunksCreated} chunks, {Duration}",
    stats.FilesProcessed, stats.ChunksCreated, stats.Duration);
```

### Health Checks
- Database connectivity
- Embedding provider availability
- Queue backpressure status
- Worker thread health

## Rollback Plan

1. **Immediate Rollback:** Switch back to Python version via configuration
2. **Data Recovery:** C# uses same schema, no data loss
3. **Log Analysis:** Compare logs between versions for root cause
4. **Gradual Rollback:** Reduce C# traffic while investigating issues

## Dependencies

### Runtime
- .NET 8.0+ (LTS)
- LanceDB .NET bindings
- Tree-sitter .NET
- Embedding provider SDKs

### Build
- .NET SDK 8.0+
- Docker for containerization
- GitHub Actions for CI/CD

## Security Considerations

- **API Keys:** Secure storage via .NET Secret Manager
- **File Access:** Minimal permissions, read-only for source code
- **Network:** HTTPS for all external API calls
- **Container:** Non-root user, minimal attack surface

## Success Criteria

- ✅ All smoke tests pass
- ✅ Performance benchmarks meet targets
- ✅ Zero data loss during migration
- ✅ Backward compatibility maintained
- ✅ Documentation updated
- ✅ Team trained on C# version

## Timeline

| Phase | Duration | Deliverables |
|-------|----------|--------------|
| Implementation | 2 weeks | Core C# codebase |
| Testing | 1 week | Full test suite |
| Integration | 1 week | Deployment pipeline |
| Migration | 1 week | Production cutover |

## Risks and Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Performance regression | Medium | High | Comprehensive benchmarking |
| Schema incompatibility | Low | High | Automated schema validation |
| Dependency issues | Medium | Medium | Containerized deployment |
| Learning curve | High | Low | Training and documentation |

## Conclusion

The C# indexing flow provides significant performance improvements while maintaining full compatibility with the existing Python ecosystem. This deployment plan ensures a smooth transition with minimal risk and clear rollback procedures.