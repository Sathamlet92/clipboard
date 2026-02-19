#!/bin/bash

echo "🗑️  Desinstalando Clipboard Manager..."
echo ""

# Buscar y restaurar el backup más reciente
BACKUP_BASE="$HOME/.clipboard-manager-backups"
if [ -d "$BACKUP_BASE" ]; then
    LATEST_BACKUP=$(ls -t "$BACKUP_BASE" 2>/dev/null | head -1)
    if [ -n "$LATEST_BACKUP" ] && [ -f "$BACKUP_BASE/$LATEST_BACKUP/restore.sh" ]; then
        echo "📦 Encontrado backup: $LATEST_BACKUP"
        read -p "¿Restaurar configuración desde backup? (s/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Ss]$ ]]; then
            echo "🔄 Restaurando backup..."
            bash "$BACKUP_BASE/$LATEST_BACKUP/restore.sh"
        else
            echo "⏭️  Saltando restauración de backup"
        fi
    fi
fi

# Matar el daemon directamente (más rápido y confiable)
if pgrep -f clipboard-daemon > /dev/null; then
    echo "⏹️  Deteniendo daemon..."
    pkill -9 -f clipboard-daemon
    sleep 1
fi

# Deshabilitar servicio
if systemctl --user is-enabled clipboard-daemon.service &> /dev/null 2>&1; then
    echo "🔧 Deshabilitando daemon..."
    systemctl --user disable clipboard-daemon.service 2>/dev/null || true
fi

# Eliminar servicio systemd
if [ -f "$HOME/.config/systemd/user/clipboard-daemon.service" ]; then
    rm "$HOME/.config/systemd/user/clipboard-daemon.service"
    echo "✅ Servicio systemd eliminado"
fi

systemctl --user daemon-reload

# Eliminar binarios
if [ -f "$HOME/.local/bin/clipboard-manager" ]; then
    rm "$HOME/.local/bin/clipboard-manager"
    echo "✅ Binario clipboard-manager eliminado"
fi

if [ -f "$HOME/.local/bin/clipboard-daemon" ]; then
    rm "$HOME/.local/bin/clipboard-daemon"
    echo "✅ Binario clipboard-daemon eliminado"
fi

# Eliminar archivos instalados
if [ -d "$HOME/.local/share/clipboard-manager" ]; then
    rm -rf "$HOME/.local/share/clipboard-manager"
    echo "✅ Archivos de aplicación eliminados"
fi

# Eliminar iconos del tema
if [ -f "$HOME/.local/share/icons/hicolor/256x256/apps/clipboard-manager.png" ]; then
    rm "$HOME/.local/share/icons/hicolor/256x256/apps/clipboard-manager.png"
    echo "✅ Icono PNG eliminado"
fi

if [ -f "$HOME/.local/share/icons/hicolor/scalable/apps/clipboard-manager.svg" ]; then
    rm "$HOME/.local/share/icons/hicolor/scalable/apps/clipboard-manager.svg"
    echo "✅ Icono SVG eliminado"
fi

# Eliminar .desktop
if [ -f "$HOME/.local/share/applications/clipboard-manager.desktop" ]; then
    rm "$HOME/.local/share/applications/clipboard-manager.desktop"
    echo "✅ Entrada de aplicación eliminada"
fi

if [ -f "$HOME/.local/share/applications/com.clipboard.manager.desktop" ]; then
    rm "$HOME/.local/share/applications/com.clipboard.manager.desktop"
    echo "✅ Entrada de aplicación eliminada"
fi

# Restaurar configuración de Hyprland
echo ""
echo "🔄 Restaurando configuración de Hyprland..."

# Eliminar reglas de ventana
WINDOWRULE_CONF="$HOME/.config/hypr/conf/windowrule.conf"
if [ -f "$WINDOWRULE_CONF" ]; then
    sed -i '/# Clipboard Manager - ventana flotante/d' "$WINDOWRULE_CONF"
    sed -i '/windowrulev2.*Clipboard Manager/d' "$WINDOWRULE_CONF"
    echo "✅ Reglas de ventana eliminadas"
fi

# Restaurar cliphist en autostart
AUTOSTART_CONF="$HOME/.config/hypr/conf/autostart.conf"
if [ -f "$AUTOSTART_CONF" ]; then
    # Descomentar cliphist
    sed -i 's/^# exec-once = wl-paste --watch cliphist store (disabled - using clipboard-manager)/exec-once = wl-paste --watch cliphist store/' "$AUTOSTART_CONF"
    
    # Eliminar líneas del daemon
    sed -i '/# Clipboard Manager Daemon/d' "$AUTOSTART_CONF"
    sed -i '/clipboard-daemon/d' "$AUTOSTART_CONF"
    
    echo "✅ autostart.conf restaurado"
fi

# Restaurar keybindings
for KEYBINDING_DIR in "keybindings" "keybinding"; do
    KEYBINDING_DEFAULT="$HOME/.config/hypr/conf/$KEYBINDING_DIR/default.conf"
    KEYBINDING_FR="$HOME/.config/hypr/conf/$KEYBINDING_DIR/fr.conf"
    
    for KEYBINDING_CONF in "$KEYBINDING_DEFAULT" "$KEYBINDING_FR"; do
        if [ -f "$KEYBINDING_CONF" ]; then
            # Restaurar cliphist.sh
            sed -i 's|bind = \$mainMod, V, exec, .*/\.local/bin/clipboard-manager.*|bind = $mainMod, V, exec, $SCRIPTS/cliphist.sh|' "$KEYBINDING_CONF"
            echo "✅ $(basename $KEYBINDING_CONF) restaurado"
        fi
    done
done

echo ""
echo "✅ Desinstalación completada!"
echo ""
echo "📝 Notas:"
echo "   - Base de datos conservada en ~/.clipboard-manager/"
echo "   - Configuración conservada en ~/.config/clipboard-manager/"
echo "   - Backups conservados en ~/.clipboard-manager-backups/"
echo ""
echo "🔄 Recarga Hyprland para aplicar cambios:"
echo "   hyprctl reload"
echo ""
echo "🗑️  Para eliminar completamente (incluyendo datos):"
echo "   rm -rf ~/.clipboard-manager"
echo "   rm -rf ~/.config/clipboard-manager"
echo "   rm -rf ~/.clipboard-manager-backups"
echo ""
