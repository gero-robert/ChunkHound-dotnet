using ChunkHound.Core;

namespace ChunkHound.Services;

/// <summary>
/// Provides language-specific configuration for chunking parameters.
/// </summary>
public class LanguageConfigProvider : ILanguageConfigProvider
{
    private readonly Dictionary<Language, LanguageConfig> _configs;

    /// <summary>
    /// Initializes a new instance of the LanguageConfigProvider class.
    /// </summary>
    public LanguageConfigProvider()
    {
        _configs = new Dictionary<Language, LanguageConfig>
        {
            [Language.CSharp] = CreateCSharpConfig(),
            [Language.Python] = CreatePythonConfig(),
            [Language.JavaScript] = CreateJavaScriptConfig(),
            [Language.TypeScript] = CreateTypeScriptConfig(),
            [Language.Java] = CreateJavaConfig(),
            [Language.Go] = CreateGoConfig(),
            [Language.Rust] = CreateRustConfig()
        };
    }

    /// <summary>
    /// Gets the language config for the specified language.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The language config.</returns>
    public LanguageConfig GetConfig(Language language)
    {
        return _configs.TryGetValue(language, out var config) ? config : CreateDefaultConfig();
    }

    private static LanguageConfig CreateCSharpConfig()
    {
        return new LanguageConfig
        {
            MaxChunkSize = 1200,
            MinChunkSize = 50,
            SafeTokenLimit = 6000,
            ChunkStartKeywords = new HashSet<string>
            {
                "using ", "namespace ", "public class ", "public interface ", "public enum ",
                "public static void Main", "public void ", "private void ", "protected void ",
                "public async Task", "private async Task", "protected async Task"
            },
            TypePatterns = new Dictionary<string, ChunkType>
            {
                ["using "] = ChunkType.Import,
                ["namespace "] = ChunkType.Module,
                ["class "] = ChunkType.Class,
                ["interface "] = ChunkType.Interface,
                ["enum "] = ChunkType.Enum,
                ["void "] = ChunkType.Function,
                ["async Task"] = ChunkType.Function
            },
            SymbolPatterns = new Dictionary<string, string>
            {
                ["class "] = "class ",
                ["interface "] = "interface ",
                ["enum "] = "enum ",
                ["namespace "] = "namespace "
            }
        };
    }

    private static LanguageConfig CreatePythonConfig()
    {
        return new LanguageConfig
        {
            MaxChunkSize = 1000, // Python tends to be more verbose
            MinChunkSize = 40,
            SafeTokenLimit = 5500,
            ChunkStartKeywords = new HashSet<string>
            {
                "import ", "from ", "class ", "def ", "if __name__"
            },
            TypePatterns = new Dictionary<string, ChunkType>
            {
                ["import "] = ChunkType.Import,
                ["from "] = ChunkType.Import,
                ["class "] = ChunkType.Class,
                ["def "] = ChunkType.Function
            },
            SymbolPatterns = new Dictionary<string, string>
            {
                ["class "] = "class ",
                ["def "] = "def "
            }
        };
    }

    private static LanguageConfig CreateJavaScriptConfig()
    {
        return new LanguageConfig
        {
            MaxChunkSize = 1100,
            MinChunkSize = 45,
            SafeTokenLimit = 5800,
            ChunkStartKeywords = new HashSet<string>
            {
                "import ", "export ", "function ", "class ", "const "
            },
            TypePatterns = new Dictionary<string, ChunkType>
            {
                ["import "] = ChunkType.Import,
                ["export "] = ChunkType.Import,
                ["function "] = ChunkType.Function,
                ["class "] = ChunkType.Class
            },
            SymbolPatterns = new Dictionary<string, string>
            {
                ["function "] = "function ",
                ["class "] = "class "
            }
        };
    }

    private static LanguageConfig CreateTypeScriptConfig()
    {
        return new LanguageConfig
        {
            MaxChunkSize = 1150,
            MinChunkSize = 48,
            SafeTokenLimit = 5900,
            ChunkStartKeywords = new HashSet<string>
            {
                "import ", "export ", "function ", "class ", "interface ", "const "
            },
            TypePatterns = new Dictionary<string, ChunkType>
            {
                ["import "] = ChunkType.Import,
                ["export "] = ChunkType.Import,
                ["function "] = ChunkType.Function,
                ["class "] = ChunkType.Class,
                ["interface "] = ChunkType.Interface
            },
            SymbolPatterns = new Dictionary<string, string>
            {
                ["function "] = "function ",
                ["class "] = "class ",
                ["interface "] = "interface "
            }
        };
    }

    private static LanguageConfig CreateJavaConfig()
    {
        return new LanguageConfig
        {
            MaxChunkSize = 1250,
            MinChunkSize = 55,
            SafeTokenLimit = 6200,
            ChunkStartKeywords = new HashSet<string>
            {
                "import ", "package ", "public class ", "public interface ", "public enum ",
                "public static void main", "public void ", "private void ", "protected void "
            },
            TypePatterns = new Dictionary<string, ChunkType>
            {
                ["import "] = ChunkType.Import,
                ["package "] = ChunkType.Module,
                ["class "] = ChunkType.Class,
                ["interface "] = ChunkType.Interface,
                ["enum "] = ChunkType.Enum,
                ["void "] = ChunkType.Function
            },
            SymbolPatterns = new Dictionary<string, string>
            {
                ["class "] = "class ",
                ["interface "] = "interface ",
                ["enum "] = "enum ",
                ["package "] = "package "
            }
        };
    }

    private static LanguageConfig CreateGoConfig()
    {
        return new LanguageConfig
        {
            MaxChunkSize = 1050,
            MinChunkSize = 42,
            SafeTokenLimit = 5700,
            ChunkStartKeywords = new HashSet<string>
            {
                "import ", "package ", "func ", "type ", "const ", "var "
            },
            TypePatterns = new Dictionary<string, ChunkType>
            {
                ["import "] = ChunkType.Import,
                ["package "] = ChunkType.Module,
                ["func "] = ChunkType.Function,
                ["type "] = ChunkType.Class
            },
            SymbolPatterns = new Dictionary<string, string>
            {
                ["func "] = "func ",
                ["type "] = "type "
            }
        };
    }

    private static LanguageConfig CreateRustConfig()
    {
        return new LanguageConfig
        {
            MaxChunkSize = 1180,
            MinChunkSize = 52,
            SafeTokenLimit = 6000,
            ChunkStartKeywords = new HashSet<string>
            {
                "use ", "mod ", "fn ", "struct ", "enum ", "impl ", "trait "
            },
            TypePatterns = new Dictionary<string, ChunkType>
            {
                ["use "] = ChunkType.Import,
                ["mod "] = ChunkType.Module,
                ["fn "] = ChunkType.Function,
                ["struct "] = ChunkType.Class,
                ["enum "] = ChunkType.Enum,
                ["impl "] = ChunkType.Class,
                ["trait "] = ChunkType.Interface
            },
            SymbolPatterns = new Dictionary<string, string>
            {
                ["fn "] = "fn ",
                ["struct "] = "struct ",
                ["enum "] = "enum ",
                ["impl "] = "impl ",
                ["trait "] = "trait "
            }
        };
    }

    private static LanguageConfig CreateDefaultConfig()
    {
        return new LanguageConfig
        {
            MaxChunkSize = 1200,
            MinChunkSize = 50,
            SafeTokenLimit = 6000,
            ChunkStartKeywords = new HashSet<string>(),
            TypePatterns = new Dictionary<string, ChunkType>(),
            SymbolPatterns = new Dictionary<string, string>()
        };
    }
}