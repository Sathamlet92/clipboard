# Configuración de Keybinding para Clipboard Manager en Hyprland

## Opción 1: Agregar bind en hyprland.conf (Recomendado)

Agrega esta línea a tu `~/.config/hypr/hyprland.conf`:

```conf
# Clipboard Manager - Super+V para mostrar/ocultar
bind = SUPER, V, exec, ~/.config/clipboard-manager/toggle-clipboard-manager.sh
```

Luego recarga Hyprland:
```bash
hyprctl reload
```

## Opción 2: Integración con ML4W

Si usas ML4W, agrega el script a tus keybindings personalizados:

1. Copia el script de toggle:
```bash
mkdir -p ~/.config/clipboard-manager
cp scripts/toggle-clipboard-manager.sh ~/.config/clipboard-manager/
chmod +x ~/.config/clipboard-manager/toggle-clipboard-manager.sh
```

2. Agrega en `~/.config/hypr/conf/keybindings.conf` o donde tengas tus bindings personalizados:
```conf
bind = SUPER, V, exec, ~/.config/clipboard-manager/toggle-clipboard-manager.sh
```

## Opción 3: Mantener la app siempre corriendo (Más simple)

Si prefieres que la app siempre esté corriendo en background:

1. Agrega a tu `~/.config/hypr/hyprland.conf` en la sección `exec-once`:
```conf
exec-once = clipboard-manager
```

2. Crea un bind simple para enfocar la ventana:
```conf
bind = SUPER, V, togglespecialworkspace, clipboard
bind = SUPER, V, focuswindow, class:^(com.clipboard.manager)$
```

## Uso

- **Super+V**: Muestra/oculta el Clipboard Manager
- **ESC**: Oculta la ventana (ya implementado en la app)
- **Super+Q**: Cierra la ventana (manejado por Hyprland)
- **Click en item**: Copia al clipboard y mantiene la ventana visible

## Notas

- La app se mantiene corriendo en background incluso cuando la ventana está oculta
- El daemon debe estar corriendo (`clipboard-daemon`)
- Puedes cerrar completamente la app con Ctrl+C en la terminal o matando el proceso
