#!/bin/bash

# Daemon simple que usa wl-paste (igual que cliphist)
# y envía los datos al servicio C# via gRPC

echo "Clipboard Daemon Simple v1.0"
echo "Usando wl-paste para capturar clipboard"

# Monitorear clipboard con wl-paste
wl-paste --watch bash -c '
    # Obtener el contenido
    CONTENT=$(wl-paste 2>/dev/null)
    
    # Obtener el tipo MIME
    MIME=$(wl-paste --list-types 2>/dev/null | head -1)
    
    # Enviar al servicio C# (aquí iría la llamada gRPC)
    echo "📋 Clipboard changed: $MIME (${#CONTENT} bytes)"
    
    # TODO: Enviar a gRPC server
'
