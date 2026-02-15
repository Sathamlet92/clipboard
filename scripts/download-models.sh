#!/bin/bash
set -e

echo "📦 Descargando modelos OCR (Tesseract Best - Alta Precisión)..."
echo ""

# Directorio de modelos
MODELS_DIR="$HOME/.clipboard-manager/models"
TESSDATA_DIR="$MODELS_DIR/tessdata"

# Crear directorios
mkdir -p "$TESSDATA_DIR"

echo "📁 Directorio de modelos: $MODELS_DIR"
echo ""

# URLs de Tesseract traineddata (best quality para mejor reconocimiento de ñ y acentos)
ENG_URL="https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata"
SPA_URL="https://github.com/tesseract-ocr/tessdata_best/raw/main/spa.traineddata"

# Descargar modelo inglés
echo "⬇️  Descargando modelo de inglés (Tesseract Best)..."
if [ -f "$TESSDATA_DIR/eng.traineddata" ]; then
    echo "♻️  Reemplazando modelo existente..."
    rm -f "$TESSDATA_DIR/eng.traineddata"
fi

if command -v wget &> /dev/null; then
    wget -O "$TESSDATA_DIR/eng.traineddata" "$ENG_URL" --progress=bar:force 2>&1
elif command -v curl &> /dev/null; then
    curl -L -o "$TESSDATA_DIR/eng.traineddata" "$ENG_URL" --progress-bar
else
    echo "❌ Error: wget o curl no encontrado. Instala uno de ellos."
    exit 1
fi
echo "✅ Modelo de inglés descargado ($(du -h "$TESSDATA_DIR/eng.traineddata" | cut -f1))"

echo ""

# Descargar modelo español
echo "⬇️  Descargando modelo de español (Tesseract Best)..."
if [ -f "$TESSDATA_DIR/spa.traineddata" ]; then
    echo "♻️  Reemplazando modelo existente..."
    rm -f "$TESSDATA_DIR/spa.traineddata"
fi

if command -v wget &> /dev/null; then
    wget -O "$TESSDATA_DIR/spa.traineddata" "$SPA_URL" --progress=bar:force 2>&1
elif command -v curl &> /dev/null; then
    curl -L -o "$TESSDATA_DIR/spa.traineddata" "$SPA_URL" --progress-bar
else
    echo "❌ Error: wget o curl no encontrado"
    exit 1
fi
echo "✅ Modelo de español descargado ($(du -h "$TESSDATA_DIR/spa.traineddata" | cut -f1))"

echo ""
echo "🎉 Modelos 'best' descargados exitosamente!"
echo "   Estos modelos tienen mejor precisión para ñ, acentos y caracteres especiales"
echo ""
echo "📊 Tamaño total:"
du -sh "$TESSDATA_DIR"
echo ""
echo "📁 Archivos:"
ls -lh "$TESSDATA_DIR"
echo ""
echo "✅ OCR listo para usar con máxima precisión!"
