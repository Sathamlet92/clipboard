#!/bin/bash
set -e

echo "📦 Descargando modelos ML para Clipboard Manager..."
echo ""

# Directorio de modelos
MODELS_DIR="$HOME/.clipboard-manager/models"
ML_DIR="$MODELS_DIR/ml"
LANG_DIR="$MODELS_DIR/language-detection"

mkdir -p "$ML_DIR"
mkdir -p "$LANG_DIR"
echo "📁 Directorio de modelos: $MODELS_DIR"
echo ""

# Función para descargar con progreso
download_file() {
    local url=$1
    local output=$2
    local name=$3
    
    echo "⬇️  Descargando $name..."
    
    if [ -f "$output" ]; then
        echo "✅ $name ya existe, omitiendo..."
        return 0
    fi
    
    if command -v wget &> /dev/null; then
        if wget -O "$output" "$url" --progress=bar:force 2>&1; then
            echo "✅ $name descargado ($(du -h "$output" | cut -f1))"
            return 0
        else
            rm -f "$output"
            return 1
        fi
    elif command -v curl &> /dev/null; then
        if curl -L -o "$output" "$url" --progress-bar; then
            echo "✅ $name descargado ($(du -h "$output" | cut -f1))"
            return 0
        else
            rm -f "$output"
            return 1
        fi
    else
        echo "❌ Error: wget o curl no encontrado"
        return 1
    fi
}

# ============================================================================
# MODELO DE EMBEDDINGS (búsqueda semántica)
# ============================================================================
echo "🌍 Descargando modelo de embeddings MULTILINGÜE..."
echo "   Modelo: paraphrase-multilingual-MiniLM-L12-v2"
echo "   Idiomas: Español, Inglés, Francés, Alemán, Italiano, Portugués, y 44+ más"
echo "   Tamaño: ~470MB"
echo "   Uso: Búsqueda por similitud semántica en múltiples idiomas"
echo ""

EMBEDDING_MODEL_URL="https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/resolve/main/onnx/model.onnx"
TOKENIZER_URL="https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/resolve/main/tokenizer.json"

download_file "$EMBEDDING_MODEL_URL" "$ML_DIR/embedding-model.onnx" "Modelo de embeddings"
download_file "$TOKENIZER_URL" "$ML_DIR/tokenizer.json" "Tokenizer"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# ============================================================================
# MODELO DE DETECCIÓN DE LENGUAJES (sin Python!)
# ============================================================================
echo "💻 Descargando modelo de detección de lenguajes de programación..."
echo "   Modelo: philomath-1209/programming-language-identification"
echo "   Base: CodeBERTa fine-tuned en Rosetta Code"
echo "   Lenguajes: 26 (C, C++, C#, Java, Python, JavaScript, Rust, Go, etc.)"
echo "   Tamaño: ~80MB"
echo "   Uso: Detección automática de lenguaje en snippets de código"
echo "   ✅ Descarga directa ONNX (sin necesidad de Python!)"
echo ""

# URLs directas del modelo ONNX ya convertido en HuggingFace
LANG_MODEL_URL="https://huggingface.co/philomath-1209/programming-language-identification/resolve/main/onnx/model.onnx"
LANG_VOCAB_URL="https://huggingface.co/philomath-1209/programming-language-identification/resolve/main/vocab.json"
LANG_MERGES_URL="https://huggingface.co/philomath-1209/programming-language-identification/resolve/main/merges.txt"
LANG_TOKENIZER_URL="https://huggingface.co/philomath-1209/programming-language-identification/resolve/main/tokenizer.json"
LANG_CONFIG_URL="https://huggingface.co/philomath-1209/programming-language-identification/resolve/main/config.json"

# Descargar archivos del modelo directamente (sin Python!)
download_file "$LANG_MODEL_URL" "$LANG_DIR/model.onnx" "Modelo ONNX"
download_file "$LANG_VOCAB_URL" "$LANG_DIR/vocab.json" "Vocabulario JSON"
download_file "$LANG_MERGES_URL" "$LANG_DIR/merges.txt" "Merges BPE"
download_file "$LANG_TOKENIZER_URL" "$LANG_DIR/tokenizer.json" "Tokenizer"
download_file "$LANG_CONFIG_URL" "$LANG_DIR/config.json" "Configuración"

# Extraer labels del config.json
if [ -f "$LANG_DIR/config.json" ]; then
    if command -v python3 &> /dev/null; then
        # Usar Python si está disponible (más confiable)
        python3 - "$LANG_DIR/config.json" "$LANG_DIR/labels.txt" << 'PYEOF'
import json, sys
with open(sys.argv[1]) as f:
    config = json.load(f)
with open(sys.argv[2], 'w') as f:
    for i in range(len(config['id2label'])):
        f.write(f"{config['id2label'][str(i)]}\n")
PYEOF
        echo "✅ Labels extraídos del config.json"
    else
        # Crear labels manualmente si no hay Python (fallback)
        cat > "$LANG_DIR/labels.txt" << 'EOF'
C
C#
C++
Java
Python
JavaScript
Rust
Go
Kotlin
Swift
Ruby
PHP
Perl
R
Lua
Scala
PowerShell
Visual Basic .NET
Pascal
COBOL
Fortran
Erlang
AppleScript
ARM Assembly
Mathematica/Wolfram Language
jq
EOF
        echo "✅ Labels creados manualmente (26 lenguajes)"
    fi
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "🎉 Modelos ML descargados exitosamente!"
echo ""
echo "📊 Tamaño total de modelos:"
du -sh "$MODELS_DIR"
echo ""
echo "✅ Modelos listos para usar!"
echo ""
echo "📝 Funcionalidades habilitadas:"
echo "  ✅ Búsqueda semántica MULTILINGÜE (50+ idiomas)"
echo "  ✅ Embeddings de 384 dimensiones"
echo "  ✅ Búsqueda híbrida (texto + semántica)"
if [ -f "$LANG_DIR/model.onnx" ]; then
    echo "  ✅ Detección automática de lenguajes de programación (26 lenguajes)"
else
    echo "  ⚠️  Detección de lenguajes: heurística"
fi
echo ""
echo "💡 Ejemplo de uso:"
echo "  Buscar: 'programación' → Encuentra: 'código', 'desarrollo', 'software'"
echo "  Buscar: 'animal' → Encuentra: 'perro', 'gato', 'mascota'"
if [ -f "$LANG_DIR/model.onnx" ]; then
    echo "  Código: detecta automáticamente C#, Java, Python, Rust, Go, etc."
fi
