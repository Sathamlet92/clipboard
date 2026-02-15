#include "clipboard_monitor.h"
#include "x11_monitor.h"

#ifdef HAVE_WAYLAND
#include "wayland_monitor.h"
#endif

#include <cstdlib>
#include <iostream>

namespace clipboard {

std::unique_ptr<IClipboardMonitor> CreateClipboardMonitor() {
    // Detect session type
    const char* session_type = std::getenv("XDG_SESSION_TYPE");
    const char* wayland_display = std::getenv("WAYLAND_DISPLAY");
    
    std::cout << "Detecting display server..." << std::endl;
    
    // Try Wayland first if available
    if ((session_type && std::string(session_type) == "wayland") || wayland_display) {
#ifdef HAVE_WAYLAND
        std::cout << "Using Wayland monitor" << std::endl;
        return std::make_unique<WaylandMonitor>();
#else
        std::cout << "Wayland detected but support not compiled in" << std::endl;
        std::cout << "Falling back to X11 (XWayland)" << std::endl;
#endif
    }
    
    // Fallback to X11
    std::cout << "Using X11 monitor" << std::endl;
    return std::make_unique<X11Monitor>();
}

} // namespace clipboard
