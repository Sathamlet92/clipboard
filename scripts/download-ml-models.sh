#!/bin/bash
set -e

echo "📦 Descargando modelos ML para Clipboard Manager..."
echo ""

# Directorio de modelos
MODELS_DIR="$HOME/.clipboard-manager/models"
ML_DIR="$MODELS_DIR/ml"

mkdir -p "$ML_DIR"
echo "📁 Directorio de modelos: $MODELS_DIR"
echo ""

# URLs de modelos
# paraphrase-multilingual-MiniLM-L12-v2: Modelo multilingüe (50+ idiomas)
# Fuente: https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2
EMBEDDING_MODEL_URL="https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/resolve/main/onnx/model.onnx"
TOKENIZER_URL="https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/resolve/main/tokenizer.json"
VOCAB_URL="https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/resolve/main/vocab.txt"

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
        wget -O "$output" "$url" --progress=bar:force 2>&1
    elif command -v curl &> /dev/null; then
        curl -L -o "$output" "$url" --progress-bar
    else
        echo "❌ Error: wget o curl no encontrado"
        exit 1
    fi
    
    if [ $? -eq 0 ]; then
        echo "✅ $name descargado ($(du -h "$output" | cut -f1))"
    else
        echo "❌ Error descargando $name"
        exit 1
    fi
}

# Descargar modelo de embeddings
echo "🌍 Descargando modelo de embeddings MULTILINGÜE..."
echo "   Modelo: paraphrase-multilingual-MiniLM-L12-v2"
echo "   Idiomas: Español, Inglés, Francés, Alemán, Italiano, Portugués, y 44+ más"
echo "   Tamaño: ~470MB"
echo "   Uso: Búsqueda por similitud semántica en múltiples idiomas"
echo ""

download_file "$EMBEDDING_MODEL_URL" "$ML_DIR/embedding-model.onnx" "Modelo de embeddings"
download_file "$TOKENIZER_URL" "$ML_DIR/tokenizer.json" "Tokenizer"
download_file "$VOCAB_URL" "$ML_DIR/vocab.txt" "Vocabulario"

echo ""
echo "🎉 Modelos ML descargados exitosamente!"
echo ""
echo "📊 Tamaño total de modelos ML:"
du -sh "$ML_DIR"
echo ""
echo "📁 Archivos descargados:"
ls -lh "$ML_DIR"
echo ""
echo "✅ Modelos listos para usar!"
echo ""
echo "📝 Funcionalidades habilitadas:"
echo "  ✅ Búsqueda semántica MULTILINGÜE (50+ idiomas)"
echo "  ✅ Embeddings de 384 dimensiones"
echo "  ✅ Búsqueda híbrida (texto + semántica)"
echo ""
echo "💡 Ejemplo de uso:"
echo "  Buscar: 'programación' → Encuentra: 'código', 'desarrollo', 'software'"
echo "  Buscar: 'animal' → Encuentra: 'perro', 'gato', 'mascota'"
