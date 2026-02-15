#pragma once

#include "clipboard_monitor.h"
#include <X11/Xlib.h>
#include <X11/Xatom.h>
#include <X11/extensions/Xfixes.h>
#include <atomic>
#include <thread>

namespace clipboard {

class X11Monitor : public IClipboardMonitor {
public:
    X11Monitor();
    ~X11Monitor() override;
    
    bool Initialize() override;
    void Run() override;
    void Stop() override;
    bool IsRunning() const override { return running_; }

private:
    void HandleSelectionNotify(const XFixesSelectionNotifyEvent& event);
    ClipboardData ReadClipboardContent();
    std::string GetActiveWindowName();
    std::string GetWindowProperty(Window window, Atom property);
    ContentType DetectContentType(const std::string& mime_type);
    
    Display* display_;
    Window window_;
    Atom clipboard_atom_;
    Atom utf8_string_atom_;
    Atom targets_atom_;
    Atom text_atom_;
    Atom png_atom_;
    
    std::atomic<bool> running_;
    int xfixes_event_base_;
};

} // namespace clipboard
