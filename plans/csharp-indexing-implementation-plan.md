# C# Indexing Flow Implementation Plan

> **Plan Version:** 1.0 | **Target:** ChunkHound C# Port | **Status:** Ready for Implementation

## Reference Documentation

The original `specs/indexing-flow.md` remains the authoritative reference for system architecture and flow concepts. All implementations must align with this specification to maintain consistency with the Python codebase.

## Executive Summary

This implementation plan outlines the systematic development of a high-performance C# port of ChunkHound's indexing pipeline. The plan emphasizes incremental development with testable milestones, comprehensive mock data infrastructure, and dependency management to achieve the target metrics: <5min indexing for 10k files, >95% embedding reuse, and <10 DB fragments.

**Key Innovations:**
- Cost-free E2E testing via `FakeConstantEmbeddingProvider`
- Incremental testable milestones with full pipeline validation
- .NET concurrency optimizations for 30%+ performance improvement

---

## Dependencies and Prerequisites

### Runtime Dependencies
- **.NET 8.0+** (LTS) - Core runtime and async patterns
- **LanceDB .NET** - Vector database with Arrow integration
- **Tree-sitter .NET** - Multi-language parsing engine
- **System.Threading.Channels** - High-performance async queues
- **Microsoft.Extensions.Logging** - Structured logging
- **System.Text.Json** - High-performance serialization

### Development Dependencies
- **xUnit** - Unit testing framework
- **Moq** - Interface mocking for unit tests
- **FluentAssertions** - Readable test assertions
- **TestContainers** - Isolated database testing
- **BenchmarkDotNet** - Performance benchmarking

### External SDK Dependencies
- **OpenAI SDK** - Production embedding provider
- **Anthropic SDK** - Alternative embedding provider
- **Ollama SDK** - Local embedding provider

---

## Implementation Phases and Testable Milestones

### Phase 1: Foundation Setup (1-2 days)
**Goal:** Establish project structure and core interfaces

**Deliverables:**
- .NET solution with proper project layout
- Core interfaces (`IEmbeddingProvider`, `IDatabaseProvider`, `IUniversalParser`)
- Basic configuration system
- Logging system that supports multiple hanlders for different outputs
- Project dependencies and NuGet packages

**Testable Milestone:** ✅ Solution builds successfully, all dependencies resolve

**Code Review Requirements:**
- **System Flow Concept:** Ensure interfaces align with the overall indexing pipeline architecture defined in `specs/indexing-flow.md`
- **Code Deduplication:** Identify and eliminate duplicate method implementations inside modules, focusing on method-level duplications rather than designing for future extensions
- **Avoid Inappropriate Defaults:** Use explicit configuration without hardcoded defaults that could conflict with production requirements
- **Best Practices:** Follow .NET naming conventions, async patterns, and dependency injection principles

**Dependencies:** None (foundation phase)

---

### Phase 2: Core Models (2-3 days)
**Design Document:** specs/csharp-indexing-flow/modules/File.cs.md

**Goal:** Implement immutable domain models with validation

**Deliverables:**
- `File.cs` - File metadata model with validation
- `Chunk.cs` - Code chunk model with serialization
- `Language.cs` - Language enumeration and extensions
- Model validation and factory methods

**Testable Milestone:** ✅ All model unit tests pass (FileTests, ChunkTests)

**Code Review Requirements:**
- **System Flow Concept:** Validate that models support the data flow requirements from file discovery to chunk storage
- **Code Deduplication:** Identify and eliminate duplicate method implementations across modules, focusing on method-level duplications rather than implementing factory methods for flexible model creation
- **Avoid Inappropriate Defaults:** Ensure validation prevents invalid states without assuming default values that might not apply
- **Best Practices:** Use immutable records, proper validation attributes, and comprehensive error messages

**Dependencies:** Phase 1 complete

---

### Phase 3: Mock Infrastructure (2-3 days)
**Goal:** Create cost-free testing infrastructure

**Deliverables:**
- `FakeConstantEmbeddingProvider.cs` - Returns constant vectors for all inputs
- Mock database provider for isolated testing
- Test data generators for files and chunks
- Benchmarking utilities

**Mock Data Setup (Reference: fake_constant_provider.py):**
```csharp
public class FakeConstantEmbeddingProvider : IEmbeddingProvider
{
    private readonly float[] _constantVector;
    private readonly int _dimensions = 1536; // OpenAI default
    private readonly float _vectorValue = 0.1f;

    public FakeConstantEmbeddingProvider()
    {
        _constantVector = Enumerable.Repeat(_vectorValue, _dimensions).ToArray();
    }

    public async Task<List<List<float>>> EmbedAsync(List<string> texts)
    {
        await Task.Delay(1); // Simulate minimal latency
        return texts.Select(_ => _constantVector.ToList()).ToList();
    }

    // Additional methods: health checks, usage tracking, etc.
}
```

**Testable Milestone:** ✅ Mock provider integration tests pass, 1000 embeddings generated in <1s

**Code Review Requirements:**
- **System Flow Concept:** Ensure mock implementations maintain the same interface contracts as production providers
- **Code Deduplication:** Identify and eliminate duplicate method implementations across modules, focusing on method-level duplications rather than designing mock infrastructure for additional test scenarios
- **Avoid Inappropriate Defaults:** Use configurable parameters instead of hardcoded test values that might not reflect real usage
- **Best Practices:** Implement proper async patterns, error handling, and resource cleanup in mock components

**Dependencies:** Phase 2 complete

---

### Phase 4: Database Layer (3-4 days)
**Design Document:** specs/csharp-indexing-flow/modules/LanceDBProvider.cs.md

**Goal:** Implement vector database with schema management

**Deliverables:**
- `LanceDBProvider.cs` - Full vector DB implementation
- Schema migration system
- ReaderWriterLockSlim for concurrent access
- Fragment optimization (<10 fragments)

**Testable Milestone:** ✅ Database integration tests pass:
- Schema creation and migration
- Concurrent read/write operations (32 threads)
- Fragment count <10 after 10k insertions

**Code Review Requirements:**
- **System Flow Concept:** Verify database operations support the chunk storage and retrieval patterns defined in the indexing flow
- **Code Deduplication:** Identify and eliminate duplicate method implementations across modules, focusing on method-level duplications rather than implementing database abstractions for different configurations
- **Avoid Inappropriate Defaults:** Configure connection settings and timeouts based on explicit requirements rather than assumptions
- **Best Practices:** Use proper connection pooling, transaction management, and error handling for database operations

**Dependencies:** Phase 1-2 complete

---

### Phase 5: Parsing Layer (3-4 days)
**Design Document:** specs/csharp-indexing-flow/modules/UniversalParser.cs.md

**Goal:** Implement multi-language parsing with cAST algorithm

**Deliverables:**
- `UniversalParser.cs` - Tree-sitter integration
- cAST chunking algorithm implementation
- Language detection and grammar loading
- Error handling for malformed code

**Testable Milestone:** ✅ Parser unit tests pass:
- Parse 100 files across 5 languages
- Chunk integrity preserved (syntactic boundaries)
- Error recovery for invalid syntax

**Code Review Requirements:**
- **System Flow Concept:** Ensure parsing logic aligns with the cAST chunking algorithm and maintains code structure integrity
- **Code Deduplication:** Identify and eliminate duplicate method implementations across modules, focusing on method-level duplications rather than designing parser for additional languages
- **Avoid Inappropriate Defaults:** Configure parsing options based on language-specific requirements rather than universal defaults
- **Best Practices:** Implement proper resource management for Tree-sitter parsers and comprehensive error handling

**Dependencies:** Phase 2 complete, Tree-sitter .NET

---

### Phase 6: Caching Layer (2-3 days)
**Design Document:** specs/csharp-indexing-flow/modules/ChunkCacheService.cs.md

**Goal:** Implement content-based diffing for embedding reuse

**Deliverables:**
- `ChunkCacheService.cs` - Content-based chunk comparison
- Normalization algorithms for consistent hashing
- Diff computation for changed content

**Testable Milestone:** ✅ Caching tests pass:
- >95% reuse rate on unchanged files
- Content normalization handles whitespace variations
- Diff computation accurate for insertions/deletions

**Code Review Requirements:**
- **System Flow Concept:** Verify caching logic supports the incremental indexing workflow and embedding reuse patterns
- **Code Deduplication:** Identify and eliminate duplicate method implementations across modules, focusing on method-level duplications rather than implementing normalization algorithms for different content types
- **Avoid Inappropriate Defaults:** Configure cache parameters based on performance requirements rather than hardcoded thresholds
- **Best Practices:** Use efficient hashing algorithms, proper memory management, and thread-safe operations

**Dependencies:** Phase 2 complete

---

### Phase 7: Worker Components (4-5 days)
**Design Documents:** specs/csharp-indexing-flow/modules/ParseWorker.cs.md, specs/csharp-indexing-flow/modules/EmbedWorker.cs.md

**Goal:** Implement concurrent pipeline workers

**Deliverables:**
- `ParseWorker.cs` - File parsing with language detection
- `EmbedWorker.cs` - Batched embedding generation
- `StoreWorker.cs` - Database storage with retry logic
- Worker lifecycle management and error isolation

**Testable Milestone:** ✅ Worker integration tests pass:
- Parallel processing of 100 files
- Graceful error handling and isolation
- Queue backpressure management

**Code Review Requirements:**
- **System Flow Concept:** Ensure worker coordination maintains the sequential flow from parsing to embedding to storage
- **Code Reusability:** Design worker base classes and interfaces that can be extended for additional processing stages
- **Avoid Inappropriate Defaults:** Configure worker pool sizes and timeouts based on system capabilities rather than fixed values
- **Best Practices:** Implement proper cancellation tokens, exception handling, and resource cleanup in async operations

**Dependencies:** Phase 3-6 complete

---

### Phase 8: Batch Processing (2-3 days)
**Design Document:** specs/csharp-indexing-flow/modules/BatchProcessor.cs.md

**Goal:** Implement dynamic batch sizing and optimization

**Deliverables:**
- `BatchProcessor.cs` - Dynamic batch size adjustment
- Performance monitoring and threshold tuning
- Error recovery and batch splitting

**Testable Milestone:** ✅ Batch processing tests pass:
- Dynamic batch sizing based on performance
- Error recovery maintains throughput
- Optimal batch sizes determined automatically

**Code Review Requirements:**
- **System Flow Concept:** Verify batch processing maintains data integrity and ordering requirements of the indexing pipeline
- **Code Reusability:** Implement configurable batch strategies that can adapt to different provider limitations and performance characteristics
- **Avoid Inappropriate Defaults:** Use adaptive algorithms instead of fixed batch sizes that may not suit all scenarios
- **Best Practices:** Implement proper monitoring, metrics collection, and graceful degradation under load

**Dependencies:** Phase 7 complete

---

### Phase 9: Embedding Service (2-3 days)
**Design Document:** specs/csharp-indexing-flow/modules/EmbeddingService.cs.md

**Goal:** Implement provider abstraction and orchestration

**Deliverables:**
- `EmbeddingService.cs` - Provider factory and orchestration
- Configuration-driven provider selection
- Batch optimization across providers

**Testable Milestone:** ✅ Embedding service tests pass:
- Provider switching without restart
- Batch optimization maintains API limits
- Error isolation between providers

**Code Review Requirements:**
- **System Flow Concept:** Ensure service orchestration maintains the embedding generation workflow and integrates properly with batch processing
- **Code Reusability:** Design provider abstraction that can accommodate different embedding APIs and configurations
- **Avoid Inappropriate Defaults:** Configure provider settings based on explicit requirements rather than assuming default behaviors
- **Best Practices:** Implement proper circuit breakers, retry logic, and rate limiting for external API calls

**Dependencies:** Phase 3, 7 complete

---

### Phase 10: Pipeline Orchestration (3-4 days)
**Goal:** Implement full indexing coordinator

**Deliverables:**
- `IndexingCoordinator.cs` - Complete pipeline orchestration
- Queue management and worker coordination
- Progress reporting and statistics collection

**Testable Milestone:** ✅ Pipeline integration tests pass:
- End-to-end indexing of test codebase
- All workers coordinate properly
- Statistics collection accurate

**Code Review Requirements:**
- **System Flow Concept:** Verify the coordinator implements the complete indexing workflow as specified in `specs/indexing-flow.md`
- **Code Reusability:** Design orchestration logic that can be extended for different indexing strategies and configurations
- **Avoid Inappropriate Defaults:** Configure pipeline parameters based on workload characteristics rather than fixed assumptions
- **Best Practices:** Implement comprehensive monitoring, progress tracking, and graceful shutdown procedures

**Dependencies:** Phase 7-9 complete

---

### Phase 11: Testing Infrastructure (3-4 days)
**Goal:** Comprehensive test coverage and CI/CD

**Deliverables:**
- Unit test suites for all components
- Integration tests with TestContainers
- Performance benchmarks vs Python baseline
- CI/CD pipeline with automated testing

**Testable Milestone:** ✅ All tests pass:
- >90% code coverage
- Performance benchmarks meet targets
- CI/CD pipeline green

**Code Review Requirements:**
- **System Flow Concept:** Ensure test suites validate the complete indexing pipeline and integration points
- **Code Reusability:** Design test utilities and fixtures that can be reused across different testing scenarios
- **Avoid Inappropriate Defaults:** Configure test environments based on explicit requirements rather than assuming default behaviors
- **Best Practices:** Implement comprehensive test coverage, proper test isolation, and automated CI/CD validation

**Dependencies:** All previous phases complete

---

### Phase 12: E2E Testing (2-3 days)
**Goal:** Full pipeline validation with realistic data

**Deliverables:**
- E2E test suite with mock data
- Performance validation against targets
- Load testing with 10k file scenarios

**Mock Data Setup for E2E:**
```csharp
public class TestDataGenerator
{
    public static List<File> GenerateTestFiles(int count = 1000)
    {
        var files = new List<File>();
        var languages = new[] { Language.CSharp, Language.Python, Language.JavaScript };

        for (int i = 0; i < count; i++)
        {
            var language = languages[i % languages.Length];
            var path = $"src/test/file_{i}.{language.GetExtension()}";
            var content = GenerateCodeSnippet(language, i);
            var hash = ComputeContentHash(content);

            files.Add(new File(
                path: path,
                mtime: DateTimeOffset.Now.ToUnixTimeSeconds(),
                language: language,
                sizeBytes: content.Length,
                contentHash: hash
            ));
        }

        return files;
    }

    public static List<Chunk> GenerateTestChunks(File file, int chunkCount = 5)
    {
        // Generate realistic chunks from file content
        // Implementation uses UniversalParser
    }
}
```

**Testable Milestone:** ✅ E2E tests pass:
- Full indexing pipeline completes <5min for 10k files
- >95% embedding reuse rate achieved
- All performance targets met

**Code Review Requirements:**
- **System Flow Concept:** Validate that E2E tests cover the complete indexing workflow from file discovery to database storage
- **Code Reusability:** Design test data generation and validation utilities that can be extended for different scenarios
- **Avoid Inappropriate Defaults:** Use realistic test data and configurations that reflect actual production usage patterns
- **Best Practices:** Implement comprehensive performance monitoring, load testing, and result validation

**Dependencies:** Phase 11 complete

---

### Phase 13: Optimization and Polish (2-3 days)
**Goal:** Performance tuning and production readiness

**Deliverables:**
- Memory usage optimization (<2GB vs Python's 4GB)
- CPU utilization improvements (60-80% vs 70-90%)
- Startup time optimization (<5s vs 10s)
- Documentation and deployment scripts

**Testable Milestone:** ✅ Production readiness validated:
- All smoke tests pass
- Performance benchmarks exceed targets
- Deployment packages created successfully

**Code Review Requirements:**
- **System Flow Concept:** Ensure optimizations maintain the integrity of the indexing pipeline and data flow
- **Code Reusability:** Implement performance tuning mechanisms that can adapt to different hardware and workload characteristics
- **Avoid Inappropriate Defaults:** Configure optimization parameters based on measured performance rather than assumptions
- **Best Practices:** Implement comprehensive monitoring, profiling, and documentation for production deployment

**Dependencies:** All previous phases complete

---

## Risk Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Performance regression | Medium | High | Comprehensive benchmarking at each phase |
| Dependency compatibility | Medium | Medium | Containerized builds, version pinning |
| Learning curve | High | Low | Detailed documentation, pair programming |
| Schema migration issues | Low | High | Automated migration testing, rollback procedures |

## Success Criteria

- ✅ **Functionality:** All Python features implemented in C#
- ✅ **Performance:** Meet or exceed all target metrics
- ✅ **Quality:** >90% test coverage, zero critical bugs
- ✅ **Compatibility:** 100% schema and API compatibility
- ✅ **Maintainability:** Clean architecture, comprehensive documentation

## Timeline and Resource Allocation

| Phase | Duration | Developers | Key Resources |
|-------|----------|------------|---------------|
| Foundation (1-2) | 1 week | 1 | .NET expertise |
| Core Implementation (3-10) | 4 weeks | 2 | Domain knowledge |
| Testing (11-12) | 2 weeks | 1-2 | Testing expertise |
| Optimization (13) | 1 week | 1 | Performance tuning |

**Total Timeline:** 8 weeks | **Total Effort:** 12-14 developer weeks

---

## Implementation Order Rationale

The implementation order follows these principles:

1. **Bottom-up Dependencies:** Start with foundations (models, interfaces) that others depend on
2. **Testability First:** Each phase delivers testable components with mock infrastructure
3. **Incremental Integration:** Build pipeline components that can be tested in isolation
4. **Risk Reduction:** Address complex dependencies (parsing, DB) early with comprehensive testing
5. **Performance Focus:** Mock infrastructure enables early performance validation

This approach ensures reliable progress with early detection of integration issues and maintains momentum through frequent testable milestones.