#include "x11_monitor.h"
#include <iostream>
#include <cstring>
#include <chrono>

namespace clipboard {

// Global error handler for X11
static int x11_error_handler(Display* display, XErrorEvent* error) {
    // Silently ignore BadWindow errors (window closed before we could read it)
    if (error->error_code == BadWindow) {
        return 0;
    }
    
    // Log other errors
    char error_text[256];
    XGetErrorText(display, error->error_code, error_text, sizeof(error_text));
    std::cerr << "X11 Error: " << error_text << std::endl;
    return 0;
}

X11Monitor::X11Monitor()
    : display_(nullptr)
    , window_(0)
    , clipboard_atom_(0)
    , utf8_string_atom_(0)
    , targets_atom_(0)
    , text_atom_(0)
    , png_atom_(0)
    , running_(false)
    , xfixes_event_base_(0)
{
}

X11Monitor::~X11Monitor() {
    Stop();
    if (window_ && display_) {
        XDestroyWindow(display_, window_);
    }
    if (display_) {
        XCloseDisplay(display_);
    }
}

bool X11Monitor::Initialize() {
    // Set custom error handler
    XSetErrorHandler(x11_error_handler);
    
    // Open display
    display_ = XOpenDisplay(nullptr);
    if (!display_) {
        std::cerr << "Failed to open X display" << std::endl;
        return false;
    }
    
    // Check XFixes extension
    int xfixes_error_base;
    if (!XFixesQueryExtension(display_, &xfixes_event_base_, &xfixes_error_base)) {
        std::cerr << "XFixes extension not available" << std::endl;
        XCloseDisplay(display_);
        display_ = nullptr;
        return false;
    }
    
    // Create invisible window for receiving events
    int screen = DefaultScreen(display_);
    window_ = XCreateSimpleWindow(
        display_,
        RootWindow(display_, screen),
        0, 0, 1, 1, 0,
        BlackPixel(display_, screen),
        WhitePixel(display_, screen)
    );
    
    if (!window_) {
        std::cerr << "Failed to create window" << std::endl;
        XCloseDisplay(display_);
        display_ = nullptr;
        return false;
    }
    
    // Get atoms
    clipboard_atom_ = XInternAtom(display_, "CLIPBOARD", False);
    utf8_string_atom_ = XInternAtom(display_, "UTF8_STRING", False);
    targets_atom_ = XInternAtom(display_, "TARGETS", False);
    text_atom_ = XInternAtom(display_, "TEXT", False);
    png_atom_ = XInternAtom(display_, "image/png", False);
    
    // Register for clipboard change notifications
    XFixesSelectSelectionInput(
        display_,
        window_,
        clipboard_atom_,
        XFixesSetSelectionOwnerNotifyMask
    );
    
    std::cout << "X11 monitor initialized successfully" << std::endl;
    return true;
}

void X11Monitor::Run() {
    if (!display_) {
        std::cerr << "Monitor not initialized" << std::endl;
        return;
    }
    
    running_ = true;
    std::cout << "X11 monitor started" << std::endl;
    
    while (running_) {
        // Check for events (with timeout)
        while (XPending(display_) > 0) {
            XEvent event;
            XNextEvent(display_, &event);
            
            if (event.type == xfixes_event_base_ + XFixesSelectionNotify) {
                auto* selection_event = reinterpret_cast<XFixesSelectionNotifyEvent*>(&event);
                HandleSelectionNotify(*selection_event);
            }
        }
        
        // Small sleep to avoid busy waiting
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }
    
    std::cout << "X11 monitor stopped" << std::endl;
}

void X11Monitor::Stop() {
    running_ = false;
}

void X11Monitor::HandleSelectionNotify(const XFixesSelectionNotifyEvent& event) {
    if (event.selection != clipboard_atom_) {
        return;
    }
    
    std::cout << "Clipboard changed" << std::endl;
    
    try {
        auto data = ReadClipboardContent();
        
        if (OnClipboardChanged) {
            OnClipboardChanged(data);
        }
    } catch (const std::exception& e) {
        std::cerr << "Error reading clipboard: " << e.what() << std::endl;
    }
}

ClipboardData X11Monitor::ReadClipboardContent() {
    ClipboardData result;
    result.timestamp = std::chrono::duration_cast<std::chrono::seconds>(
        std::chrono::system_clock::now().time_since_epoch()
    ).count();
    
    // Get active window info
    result.source_app = GetActiveWindowName();
    result.window_title = result.source_app; // Simplified for now
    
    // Request clipboard content
    XConvertSelection(
        display_,
        clipboard_atom_,
        utf8_string_atom_,
        clipboard_atom_,
        window_,
        CurrentTime
    );
    
    XFlush(display_);
    
    // Wait for SelectionNotify event
    XEvent event;
    bool received = false;
    auto start = std::chrono::steady_clock::now();
    
    while (!received && 
           std::chrono::steady_clock::now() - start < std::chrono::seconds(1)) {
        if (XCheckTypedWindowEvent(display_, window_, SelectionNotify, &event)) {
            received = true;
        } else {
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
        }
    }
    
    if (!received) {
        throw std::runtime_error("Timeout waiting for clipboard data");
    }
    
    // Read the property
    Atom actual_type;
    int actual_format;
    unsigned long nitems, bytes_after;
    unsigned char* prop_data = nullptr;
    
    int status = XGetWindowProperty(
        display_,
        window_,
        clipboard_atom_,
        0, ~0L, False,
        AnyPropertyType,
        &actual_type,
        &actual_format,
        &nitems,
        &bytes_after,
        &prop_data
    );
    
    if (status != Success || !prop_data) {
        throw std::runtime_error("Failed to read clipboard property");
    }
    
    // Copy data
    result.data.assign(prop_data, prop_data + nitems);
    XFree(prop_data);
    
    // Determine MIME type
    if (actual_type == utf8_string_atom_ || actual_type == XA_STRING) {
        result.mime_type = "text/plain";
        result.content_type = ContentType::TEXT;
    } else if (actual_type == png_atom_) {
        result.mime_type = "image/png";
        result.content_type = ContentType::IMAGE;
    } else {
        result.mime_type = "application/octet-stream";
        result.content_type = ContentType::UNKNOWN;
    }
    
    return result;
}

std::string X11Monitor::GetActiveWindowName() {
    // Simplified: just return "clipboard" for now
    // Getting active window in XWayland can be unreliable
    return "clipboard";
}

std::string X11Monitor::GetWindowProperty(Window window, Atom property) {
    Atom actual_type;
    int actual_format;
    unsigned long nitems, bytes_after;
    unsigned char* prop_data = nullptr;
    
    // Sync and check if window is valid
    XSync(display_, False);
    XWindowAttributes attrs;
    if (XGetWindowAttributes(display_, window, &attrs) == 0) {
        return "";
    }
    
    int status = XGetWindowProperty(
        display_, window,
        property,
        0, 1024, False,
        AnyPropertyType,
        &actual_type,
        &actual_format,
        &nitems,
        &bytes_after,
        &prop_data
    );
    
    if (status != Success || !prop_data) {
        return "";
    }
    
    std::string result(reinterpret_cast<char*>(prop_data));
    XFree(prop_data);
    
    return result;
}

ContentType X11Monitor::DetectContentType(const std::string& mime_type) {
    if (mime_type.find("text/") == 0) {
        return ContentType::TEXT;
    } else if (mime_type.find("image/") == 0) {
        return ContentType::IMAGE;
    } else if (mime_type.find("text/html") == 0) {
        return ContentType::HTML;
    }
    return ContentType::UNKNOWN;
}

} // namespace clipboard
