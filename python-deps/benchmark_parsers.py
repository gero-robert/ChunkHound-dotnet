#!/usr/bin/env python3
"""
Benchmark script for Python ChunkHound parsers.
This script measures parsing, splitting, and embedding performance for comparison with .NET implementation.
"""

import os
import sys
import time
import json
from pathlib import Path

# Add the project root to Python path
sys.path.insert(0, str(Path(__file__).parent.parent))

def benchmark_file_parsing(file_path: str, parser_name: str) -> dict:
    """Benchmark parsing a single file."""
    start_time = time.time()

    try:
        # Import here to avoid import errors if modules not available
        if parser_name == "markdown":
            from chunkhound.parsers.markdown_parser import MarkdownParser
            parser = MarkdownParser()
        elif parser_name == "yaml":
            from chunkhound.parsers.rapid_yaml_parser import RapidYamlParser
            parser = RapidYamlParser()
        elif parser_name == "vue":
            from chunkhound.parsers.vue_parser import VueParser
            parser = VueParser()
        elif parser_name == "code":
            from chunkhound.parsers.code_parser import CodeParser
            parser = CodeParser()
        else:
            raise ValueError(f"Unknown parser: {parser_name}")

        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()

        chunks = parser.parse(content, file_path)

        parse_time = time.time() - start_time

        return {
            "file": os.path.basename(file_path),
            "parser": parser_name,
            "parse_time_ms": parse_time * 1000,
            "chunk_count": len(chunks),
            "total_chunk_size": sum(len(chunk.content) for chunk in chunks),
            "avg_chunk_size": sum(len(chunk.content) for chunk in chunks) / len(chunks) if chunks else 0,
            "success": True
        }

    except Exception as e:
        return {
            "file": os.path.basename(file_path),
            "parser": parser_name,
            "parse_time_ms": (time.time() - start_time) * 1000,
            "error": str(e),
            "success": False
        }

def benchmark_embedding(texts: list, provider_name: str = "fake") -> dict:
    """Benchmark embedding generation."""
    start_time = time.time()

    try:
        if provider_name == "fake":
            # Use a simple fake embedding for benchmarking
            embeddings = []
            for text in texts:
                # Simulate embedding generation
                import hashlib
                hash_obj = hashlib.md5(text.encode())
                embedding = [int(hash_obj.hexdigest()[i:i+2], 16) / 255.0 for i in range(0, 32, 2)]
                embeddings.append(embedding)
        else:
            # Could add real embedding providers here
            raise ValueError(f"Unknown embedding provider: {provider_name}")

        embed_time = time.time() - start_time

        return {
            "embedding_provider": provider_name,
            "text_count": len(texts),
            "embed_time_ms": embed_time * 1000,
            "avg_embed_time_per_text_ms": (embed_time * 1000) / len(texts) if texts else 0,
            "success": True
        }

    except Exception as e:
        return {
            "embedding_provider": provider_name,
            "text_count": len(texts),
            "embed_time_ms": (time.time() - start_time) * 1000,
            "error": str(e),
            "success": False
        }

def main():
    """Main benchmark function."""
    # Test files directory
    test_files_dir = Path(__file__).parent.parent / "ChunkHound.Tests" / "Benchmarks" / "TestFiles"

    if not test_files_dir.exists():
        print(f"Test files directory not found: {test_files_dir}")
        return

    results = {
        "timestamp": time.time(),
        "python_version": sys.version,
        "parsing_results": [],
        "embedding_results": []
    }

    # Benchmark parsing for each file type
    test_files = [
        ("sample.md", "markdown"),
        ("sample.yaml", "yaml"),
        ("sample.vue", "vue"),
        ("sample.cs", "code")
    ]

    all_texts = []

    for filename, parser_name in test_files:
        file_path = test_files_dir / filename
        if file_path.exists():
            print(f"Benchmarking {filename} with {parser_name} parser...")
            result = benchmark_file_parsing(str(file_path), parser_name)
            results["parsing_results"].append(result)

            if result["success"]:
                # Collect texts for embedding benchmark
                try:
                    if parser_name == "markdown":
                        from chunkhound.parsers.markdown_parser import MarkdownParser
                        parser = MarkdownParser()
                    elif parser_name == "yaml":
                        from chunkhound.parsers.rapid_yaml_parser import RapidYamlParser
                        parser = RapidYamlParser()
                    elif parser_name == "vue":
                        from chunkhound.parsers.vue_parser import VueParser
                        parser = VueParser()
                    elif parser_name == "code":
                        from chunkhound.parsers.code_parser import CodeParser
                        parser = CodeParser()

                    with open(file_path, 'r', encoding='utf-8') as f:
                        content = f.read()

                    chunks = parser.parse(content, str(file_path))
                    all_texts.extend([chunk.content for chunk in chunks])
                except Exception as e:
                    print(f"Error collecting texts for {filename}: {e}")
        else:
            print(f"Test file not found: {file_path}")

    # Benchmark embedding
    if all_texts:
        print(f"Benchmarking embedding for {len(all_texts)} texts...")
        embed_result = benchmark_embedding(all_texts)
        results["embedding_results"].append(embed_result)

    # Save results
    output_file = Path(__file__).parent / "benchmark_results.json"
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2)

    print(f"Benchmark results saved to: {output_file}")

    # Print summary
    print("\n=== PYTHON BENCHMARK SUMMARY ===")
    for result in results["parsing_results"]:
        if result["success"]:
            print(f"{result['file']}: {result['parse_time_ms']:.2f}ms, {result['chunk_count']} chunks")
        else:
            print(f"{result['file']}: FAILED - {result.get('error', 'Unknown error')}")

    for result in results["embedding_results"]:
        if result["success"]:
            print(f"Embedding: {result['embed_time_ms']:.2f}ms for {result['text_count']} texts")
        else:
            print(f"Embedding: FAILED - {result.get('error', 'Unknown error')}")

if __name__ == "__main__":
    main()