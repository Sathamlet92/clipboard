#!/bin/bash
set -e

echo "📦 Instalando Clipboard Manager para Arch Linux + Hyprland"
echo ""

# Verificar que estamos en Arch Linux
if ! command -v pacman &> /dev/null; then
    echo "❌ Este script es solo para Arch Linux"
    exit 1
fi

# Crear backup de configuración de Hyprland
echo "💾 Creando backup de configuración de Hyprland..."
BACKUP_DIR="$HOME/.clipboard-manager-backups/$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

# Backup de archivos que vamos a modificar
if [ -f "$HOME/.config/hypr/conf/autostart.conf" ]; then
    mkdir -p "$BACKUP_DIR/conf"
    cp "$HOME/.config/hypr/conf/autostart.conf" "$BACKUP_DIR/conf/autostart.conf"
    echo "✅ Backup: autostart.conf"
fi

# Backup de keybindings (buscar en ambos directorios posibles)
for KEYBINDING_DIR in "keybindings" "keybinding"; do
    if [ -d "$HOME/.config/hypr/conf/$KEYBINDING_DIR" ]; then
        mkdir -p "$BACKUP_DIR/conf/$KEYBINDING_DIR"
        for file in default.conf fr.conf; do
            if [ -f "$HOME/.config/hypr/conf/$KEYBINDING_DIR/$file" ]; then
                cp "$HOME/.config/hypr/conf/$KEYBINDING_DIR/$file" "$BACKUP_DIR/conf/$KEYBINDING_DIR/$file"
                echo "✅ Backup: $KEYBINDING_DIR/$file"
            fi
        done
    fi
done

# Crear script de restauración
cat > "$BACKUP_DIR/restore.sh" << 'RESTORE_EOF'
#!/bin/bash
echo "🔄 Restaurando configuración de Hyprland..."
BACKUP_DIR="$(cd "$(dirname "$0")" && pwd)"

if [ -d "$BACKUP_DIR/conf" ]; then
    cp -r "$BACKUP_DIR/conf"/* "$HOME/.config/hypr/conf/"
    echo "✅ Configuración restaurada"
    echo "🔄 Recarga Hyprland con: hyprctl reload"
else
    echo "❌ No se encontraron archivos de backup"
fi
RESTORE_EOF

chmod +x "$BACKUP_DIR/restore.sh"

echo "📁 Backup guardado en: $BACKUP_DIR"
echo "   Para restaurar: $BACKUP_DIR/restore.sh"
echo ""

# Instalar dependencias del sistema
echo "📥 Instalando dependencias del sistema..."
sudo pacman -S --needed --noconfirm \
    gtk4 \
    gtkmm-4.0 \
    sqlite \
    tesseract \
    tesseract-data-eng \
    tesseract-data-spa \
    opencv \
    wl-clipboard \
    grpc \
    protobuf \
    onnxruntime \
    nlohmann-json \
    cmake \
    ninja

# Crear directorios
echo "📁 Creando directorios..."
INSTALL_DIR="$HOME/.local/share/clipboard-manager"
BIN_DIR="$HOME/.local/bin"
CONFIG_DIR="$HOME/.config/clipboard-manager"

mkdir -p "$INSTALL_DIR"
mkdir -p "$BIN_DIR"
mkdir -p "$CONFIG_DIR"
mkdir -p "$HOME/.clipboard-manager/models"

# Copiar tema por defecto si no existe
if [ ! -f "$CONFIG_DIR/theme.json" ]; then
    echo "🎨 Copiando tema por defecto..."
    cp themes/cliphist-style.json "$CONFIG_DIR/theme.json" 2>/dev/null || true
    echo "✅ Tema instalado en $CONFIG_DIR/theme.json"
fi

# Compilar la aplicación C++
echo "🔨 Compilando aplicación C++ (Wayland nativo)..."
cd cpp-app
./build.sh
cd ..

# Copiar binario
cp cpp-app/build/clipboard-manager "$BIN_DIR/"

# Compilar el daemon C++
echo "🔨 Compilando daemon..."
cd daemon
./build.sh
cd ..

# Copiar daemon
cp daemon/build/clipboard-daemon "$BIN_DIR/"

# Descargar modelos ML
echo "📥 Descargando modelos ML..."
bash scripts/download-ml-models.sh

# Crear script de inicio para la app (ya no necesario, es nativo)
# El binario ya está en $BIN_DIR/clipboard-manager

# Crear servicio systemd para el daemon
echo "🔧 Configurando servicio systemd..."
mkdir -p "$HOME/.config/systemd/user"
cat > "$HOME/.config/systemd/user/clipboard-daemon.service" << EOF
[Unit]
Description=Clipboard Manager Daemon
After=graphical-session.target

[Service]
Type=simple
ExecStart=$HOME/.local/bin/clipboard-daemon
Restart=on-failure
RestartSec=5

[Install]
WantedBy=default.target
EOF

# Habilitar e iniciar el daemon
systemctl --user daemon-reload
systemctl --user enable clipboard-daemon.service
systemctl --user start clipboard-daemon.service

# Crear archivo .desktop
echo "�️  Creando entrada de aplicación..."
mkdir -p "$HOME/.local/share/applications"
cat > "$HOME/.local/share/applications/clipboard-manager.desktop" << EOF
[Desktop Entry]
Name=Clipboard Manager
Comment=Smart clipboard manager with OCR and ML (Native Wayland)
Exec=$HOME/.local/bin/clipboard-manager
Icon=edit-paste
Terminal=false
Type=Application
Categories=Utility;
EOF

# Configuración para Hyprland
echo "⚙️  Configurando Hyprland..."

# Deshabilitar cliphist en autostart
AUTOSTART_CONF="$HOME/.config/hypr/conf/autostart.conf"
if [ -f "$AUTOSTART_CONF" ]; then
    if grep -q "cliphist" "$AUTOSTART_CONF"; then
        echo "🔧 Deshabilitando cliphist en autostart..."
        sed -i 's/^exec-once = wl-paste --watch cliphist store/# exec-once = wl-paste --watch cliphist store (disabled - using clipboard-manager)/' "$AUTOSTART_CONF"
    fi
    
    # Agregar nuestro daemon
    if ! grep -q "clipboard-daemon" "$AUTOSTART_CONF"; then
        echo "" >> "$AUTOSTART_CONF"
        echo "# Clipboard Manager Daemon" >> "$AUTOSTART_CONF"
        echo "exec-once = $HOME/.local/bin/clipboard-daemon" >> "$AUTOSTART_CONF"
        echo "✅ Daemon agregado a autostart.conf"
    fi
fi

# Reemplazar keybinding de cliphist
# Buscar en ambos directorios posibles: keybindings/ y keybinding/
for KEYBINDING_DIR in "keybindings" "keybinding"; do
    KEYBINDING_DEFAULT="$HOME/.config/hypr/conf/$KEYBINDING_DIR/default.conf"
    KEYBINDING_FR="$HOME/.config/hypr/conf/$KEYBINDING_DIR/fr.conf"
    
    for KEYBINDING_CONF in "$KEYBINDING_DEFAULT" "$KEYBINDING_FR"; do
        if [ -f "$KEYBINDING_CONF" ]; then
            if grep -q "cliphist.sh" "$KEYBINDING_CONF"; then
                echo "🔧 Reemplazando keybinding en $KEYBINDING_CONF..."
                sed -i 's|bind = \$mainMod, V, exec, \$SCRIPTS/cliphist.sh.*|bind = $mainMod, V, exec, '"$HOME"'/.local/bin/clipboard-manager  # Clipboard Manager|' "$KEYBINDING_CONF"
                echo "✅ Keybinding actualizado: Super+V"
            fi
        fi
    done
done

echo ""
echo "✅ Instalación completada!"
echo ""
echo "📝 Uso:"
echo "   - El daemon se inicia automáticamente"
echo "   - Presiona Super+V para abrir el manager"
echo "   - O ejecuta: clipboard-manager"
echo ""
echo "🔧 Comandos útiles:"
echo "   systemctl --user status clipboard-daemon  # Ver estado"
echo "   systemctl --user restart clipboard-daemon # Reiniciar"
echo "   systemctl --user stop clipboard-daemon    # Detener"
echo ""
echo "� Aplicación C++ con Wayland nativo (GTK4) - Sin XWayland!"
echo ""
echo "⚠️  IMPORTANTE: Recarga Hyprland para aplicar cambios:"
echo "   hyprctl reload"
echo ""
