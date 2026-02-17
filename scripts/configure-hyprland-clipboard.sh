#!/bin/bash

set -e

DRY_RUN=1
if [ "${1:-}" = "--apply" ]; then
    DRY_RUN=0
fi

HYPR_DIR="$HOME/.config/hypr"
HYPR_CONF="$HYPR_DIR/hyprland.conf"
ML4W_CONF="$HYPR_DIR/conf/ml4w.conf"
KEYBINDING_CONF_POINTER="$HYPR_DIR/conf/keybinding.conf"
DEFAULT_KB_FILE="$HYPR_DIR/conf/keybindings/default.conf"
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT_BUILD_BIN="$PROJECT_ROOT/cpp-app/build/clipboard-manager"

if [ ! -f "$HYPR_CONF" ]; then
    echo "❌ No se encontró $HYPR_CONF"
    exit 1
fi

if [ -x "$HOME/.local/bin/clipboard-manager" ]; then
    CLIP_CMD="$HOME/.local/bin/clipboard-manager"
elif [ -x "/usr/local/bin/clipboard-manager" ]; then
    CLIP_CMD="/usr/local/bin/clipboard-manager"
elif [ -x "$PROJECT_BUILD_BIN" ]; then
    CLIP_CMD="$PROJECT_BUILD_BIN"
else
    CLIP_CMD="clipboard-manager"
fi

ACTIVE_KB_FILE="$DEFAULT_KB_FILE"
if [ -f "$KEYBINDING_CONF_POINTER" ]; then
    SRC_LINE=$(grep -E '^source\s*=\s*' "$KEYBINDING_CONF_POINTER" | head -n1 || true)
    if [ -n "$SRC_LINE" ]; then
        SRC_PATH=$(echo "$SRC_LINE" | sed -E 's/^source\s*=\s*//')
        SRC_PATH=${SRC_PATH/#\~/$HOME}
        if [ -f "$SRC_PATH" ]; then
            ACTIVE_KB_FILE="$SRC_PATH"
        fi
    fi
fi

BACKUP_DIR="$HOME/.clipboard-manager-backups/$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

backup_file() {
    local file="$1"
    if [ -f "$file" ]; then
        local rel="${file#$HYPR_DIR/}"
        local target="$BACKUP_DIR/$rel"
        mkdir -p "$(dirname "$target")"
        cp "$file" "$target"
        echo "✅ Backup: $rel"
    fi
}

echo "🔧 Configurando Clipboard Manager para Hyprland"
if [ $DRY_RUN -eq 1 ]; then
    echo "(Modo simulación - sin cambios)"
fi
echo ""

echo "💾 Creando backup..."
backup_file "$HYPR_CONF"
backup_file "$ML4W_CONF"
backup_file "$KEYBINDING_CONF_POINTER"
backup_file "$ACTIVE_KB_FILE"

cat > "$BACKUP_DIR/restore.sh" << 'EOF'
#!/bin/bash
set -e
echo "🔄 Restaurando configuración de Hyprland..."
BACKUP_DIR="$(cd "$(dirname "$0")" && pwd)"
HYPR_DIR="$HOME/.config/hypr"

if [ -f "$BACKUP_DIR/hyprland.conf" ]; then
  cp "$BACKUP_DIR/hyprland.conf" "$HYPR_DIR/hyprland.conf"
  echo "✅ Restaurado: hyprland.conf"
fi

if [ -d "$BACKUP_DIR/conf" ]; then
  find "$BACKUP_DIR/conf" -type f | while read -r f; do
    rel="${f#$BACKUP_DIR/}"
    dst="$HYPR_DIR/$rel"
    mkdir -p "$(dirname "$dst")"
    cp "$f" "$dst"
    echo "✅ Restaurado: $rel"
  done
fi

echo "🔄 Ejecuta: hyprctl reload"
EOF
chmod +x "$BACKUP_DIR/restore.sh"

if [ ! -f "$ML4W_CONF" ]; then
    echo "⚠️  No existe ml4w.conf, usaré hyprland.conf para windowrule"
    ML4W_CONF="$HYPR_CONF"
fi

echo "📝 Archivos objetivo:"
echo "   Window rule: ${ML4W_CONF#$HOME/}"
echo "   Keybinding:  ${ACTIVE_KB_FILE#$HOME/}"
echo "   Comando:     $CLIP_CMD"
echo ""

echo "📋 Cambios:"
echo "1) Regla flotante para class com.clipboard.manager"
echo "2) Super+V apuntando a $CLIP_CMD"
echo "3) Limpieza de binds duplicados/conflictivos"
echo ""

if [ "$CLIP_CMD" = "clipboard-manager" ]; then
    echo "⚠️  Aviso: no se encontró binario instalado en ~/.local/bin ni /usr/local/bin"
    echo "   Puedes instalar con install-arch.sh o copiar el binario compilado."
    echo ""
fi

if [ $DRY_RUN -eq 0 ]; then
    echo "✏️  Aplicando..."

    if ! grep -q "match:class = (com.clipboard.manager)" "$ML4W_CONF"; then
        cat >> "$ML4W_CONF" << 'RULE'

# Clipboard Manager
windowrule {
    name = clipboard-manager
    match:class = (com.clipboard.manager)
    float = true
    center = true
    size = 600 500
}
RULE
        echo "✅ Window rule agregado"
    else
        echo "ℹ️  Window rule ya existía"
    fi

    sed -i '/bind\s*=\s*\$mainMod,\s*V,\s*exec,\s*.*cliphist/d' "$HYPR_CONF" "$ACTIVE_KB_FILE" 2>/dev/null || true
    sed -i '/bind\s*=\s*\$mainMod,\s*V,\s*exec,\s*.*clipboard-manager/d' "$HYPR_CONF" "$ACTIVE_KB_FILE" 2>/dev/null || true
    sed -i '/bind\s*=\s*SUPER,\s*V,\s*exec,\s*.*clipboard-manager/d' "$HYPR_CONF" "$ACTIVE_KB_FILE" 2>/dev/null || true

    echo "" >> "$ACTIVE_KB_FILE"
    echo "bind = \$mainMod, V, exec, $CLIP_CMD  # Clipboard Manager" >> "$ACTIVE_KB_FILE"
    echo "✅ Keybinding actualizado en ${ACTIVE_KB_FILE#$HOME/}"

    echo ""
    echo "✅ Listo. Recarga con: hyprctl reload"
else
    echo "Para aplicar:"
    echo "  $0 --apply"
fi

echo ""
echo "📁 Backup: $BACKUP_DIR"
echo "🔄 Restore: $BACKUP_DIR/restore.sh"
echo ""
