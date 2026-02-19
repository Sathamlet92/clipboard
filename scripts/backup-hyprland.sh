#!/bin/bash

echo "💾 Creando backup de configuración de Hyprland..."
echo ""

# Crear directorio de backup con timestamp
BACKUP_DIR="$HOME/.clipboard-manager-backups/$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

echo "📁 Directorio de backup: $BACKUP_DIR"
echo ""

# Backup de archivos de configuración de Hyprland
echo "📋 Respaldando configuración de Hyprland..."

# Backup completo del directorio conf
if [ -d "$HOME/.config/hypr/conf" ]; then
    cp -r "$HOME/.config/hypr/conf" "$BACKUP_DIR/conf"
    echo "✅ Respaldado: ~/.config/hypr/conf/"
fi

# Backup del archivo principal
if [ -f "$HOME/.config/hypr/hyprland.conf" ]; then
    cp "$HOME/.config/hypr/hyprland.conf" "$BACKUP_DIR/hyprland.conf"
    echo "✅ Respaldado: ~/.config/hypr/hyprland.conf"
fi

# Backup de archivos específicos que vamos a modificar
FILES_TO_BACKUP=(
    "$HOME/.config/hypr/conf/autostart.conf"
    "$HOME/.config/hypr/conf/keybindings/default.conf"
    "$HOME/.config/hypr/conf/keybindings/fr.conf"
    "$HOME/.config/hypr/conf/keybinding/default.conf"
    "$HOME/.config/hypr/conf/keybinding/fr.conf"
)

echo ""
echo "📄 Archivos específicos respaldados:"
for file in "${FILES_TO_BACKUP[@]}"; do
    if [ -f "$file" ]; then
        # Crear estructura de directorios en el backup
        rel_path="${file#$HOME/.config/hypr/}"
        backup_path="$BACKUP_DIR/$rel_path"
        mkdir -p "$(dirname "$backup_path")"
        cp "$file" "$backup_path"
        echo "  ✅ $(basename $file)"
    fi
done

# Crear script de restauración
cat > "$BACKUP_DIR/restore.sh" << 'EOF'
#!/bin/bash
echo "🔄 Restaurando configuración de Hyprland..."
echo ""

BACKUP_DIR="$(cd "$(dirname "$0")" && pwd)"

# Restaurar directorio conf completo
if [ -d "$BACKUP_DIR/conf" ]; then
    echo "📋 Restaurando ~/.config/hypr/conf/..."
    cp -r "$BACKUP_DIR/conf" "$HOME/.config/hypr/"
    echo "✅ Restaurado"
fi

# Restaurar hyprland.conf
if [ -f "$BACKUP_DIR/hyprland.conf" ]; then
    echo "📋 Restaurando hyprland.conf..."
    cp "$BACKUP_DIR/hyprland.conf" "$HOME/.config/hypr/hyprland.conf"
    echo "✅ Restaurado"
fi

echo ""
echo "✅ Restauración completada!"
echo "🔄 Recarga Hyprland con: hyprctl reload"
EOF

chmod +x "$BACKUP_DIR/restore.sh"

# Crear archivo de información
cat > "$BACKUP_DIR/INFO.txt" << EOF
Backup de configuración de Hyprland
Fecha: $(date)
Usuario: $USER
Hostname: $(hostname)

Archivos respaldados:
- ~/.config/hypr/conf/ (completo)
- ~/.config/hypr/hyprland.conf

Para restaurar:
  cd $BACKUP_DIR
  ./restore.sh

O manualmente:
  cp -r $BACKUP_DIR/conf ~/.config/hypr/
  cp $BACKUP_DIR/hyprland.conf ~/.config/hypr/
  hyprctl reload
EOF

echo ""
echo "✅ Backup completado!"
echo ""
echo "📁 Ubicación: $BACKUP_DIR"
echo ""
echo "🔄 Para restaurar en caso de problemas:"
echo "   cd $BACKUP_DIR"
echo "   ./restore.sh"
echo ""
echo "📝 Archivos respaldados:"
find "$BACKUP_DIR" -type f -not -name "restore.sh" -not -name "INFO.txt" | while read file; do
    echo "   - ${file#$BACKUP_DIR/}"
done
echo ""
