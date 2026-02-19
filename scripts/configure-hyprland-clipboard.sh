#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "$SCRIPT_DIR/lib/common.sh"

DRY_RUN=1
if [ "${1:-}" = "--apply" ]; then
    DRY_RUN=0
fi

HYPR_DIR="$HOME/.config/hypr"
HYPR_CONF="$HYPR_DIR/hyprland.conf"
ML4W_CONF="$HYPR_DIR/conf/ml4w.conf"
KEYBINDING_CONF_POINTER="$HYPR_DIR/conf/keybinding.conf"
DEFAULT_KB_FILE="$HYPR_DIR/conf/keybindings/default.conf"
PROJECT_ROOT="$(project_root_dir)"

if [ ! -f "$HYPR_CONF" ]; then
    log_err "No se encontró $HYPR_CONF"
    exit 1
fi

CLIP_CMD="$(detect_clipboard_binary "$PROJECT_ROOT")"
ACTIVE_KB_FILE="$(detect_active_keybinding_file "$HYPR_DIR")"
BACKUP_DIR="$(create_hypr_backup_dir)"
mkdir -p "$BACKUP_DIR"

echo "🔧 Configurando Clipboard Manager para Hyprland"
if [ $DRY_RUN -eq 1 ]; then
    echo "(Modo simulación - sin cambios)"
fi
echo ""

echo "💾 Creando backup..."
backup_hypr_file "$HYPR_DIR" "$BACKUP_DIR" "$HYPR_CONF"
backup_hypr_file "$HYPR_DIR" "$BACKUP_DIR" "$ML4W_CONF"
backup_hypr_file "$HYPR_DIR" "$BACKUP_DIR" "$KEYBINDING_CONF_POINTER"
backup_hypr_file "$HYPR_DIR" "$BACKUP_DIR" "$ACTIVE_KB_FILE"

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
    log_warn "No existe ml4w.conf, usaré hyprland.conf para windowrule"
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
    log_warn "Aviso: no se encontró binario instalado en ~/.local/bin ni /usr/local/bin"
    echo "   Puedes instalar con install-cpp.sh o copiar el binario compilado."
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
