# Temas de Clipboard Manager

## Uso

Copia uno de estos archivos a `~/.config/clipboard-manager/theme.json` para aplicar el tema.

```bash
# Aplicar tema estilo cliphist
cp themes/cliphist-style.json ~/.config/clipboard-manager/theme.json

# Aplicar tema por defecto
cp themes/default.json ~/.config/clipboard-manager/theme.json
```

Luego reinicia la aplicación.

## Personalización

Puedes editar `~/.config/clipboard-manager/theme.json` directamente para personalizar:

### Window (Ventana)
- `width`: Ancho en píxeles (default: 600)
- `height`: Alto en píxeles (default: 500)
- `cornerRadius`: Radio de esquinas redondeadas (default: 12)
- `borderThickness`: Grosor del borde (default: 2)
- `opacity`: Transparencia 0.0-1.0 (default: 0.95)

### Colors (Colores)
Todos los colores en formato hexadecimal `#RRGGBB`:
- `background`: Fondo principal
- `backgroundAlt`: Fondo alternativo (items)
- `border`: Color de bordes
- `accent`: Color de acento (badges, botones)
- `text`: Color de texto principal
- `textSecondary`: Color de texto secundario (metadata)
- `searchBar`: Fondo de la barra de búsqueda
- `itemHover`: Color al pasar el mouse sobre items
- `codeBackground`: Fondo de bloques de código
- `urlBackground`: Fondo de URLs
- `urlText`: Color de texto de URLs
- `ocrBackground`: Fondo de texto OCR
- `ocrText`: Color de texto OCR

### Fonts (Fuentes)
- `family`: Familia de fuente principal
- `monoFamily`: Familia de fuente monoespaciada (código)
- `size`: Tamaño base (default: 13)
- `sizeSmall`: Tamaño pequeño (default: 11)
- `sizeLarge`: Tamaño grande (default: 14)

### Spacing (Espaciado)
- `itemPadding`: Padding interno de items (default: 10)
- `itemMargin`: Margen entre items (default: 5)
- `itemSpacing`: Espaciado entre elementos (default: 5)

## Temas Incluidos

### cliphist-style.json
Tema inspirado en cliphist con tonos verdes/turquesa y transparencia.

### default.json
Tema por defecto con estilo VS Code oscuro.
