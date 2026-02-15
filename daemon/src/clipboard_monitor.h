#pragma once

#include <functional>
#include <memory>
#include <string>
#include <vector>

namespace clipboard {

enum class ContentType {
    UNKNOWN = 0,
    TEXT = 1,
    IMAGE = 2,
    HTML = 3,
    FILE = 4
};

struct ClipboardData {
    std::vector<uint8_t> data;
    std::string mime_type;
    ContentType content_type;
    std::string source_app;
    std::string window_title;
    int64_t timestamp;
};

// Interface for clipboard monitoring
class IClipboardMonitor {
public:
    virtual ~IClipboardMonitor() = default;
    
    virtual bool Initialize() = 0;
    virtual void Run() = 0;
    virtual void Stop() = 0;
    virtual bool IsRunning() const = 0;
    
    // Callback when clipboard changes
    std::function<void(const ClipboardData&)> OnClipboardChanged;
};

// Factory function
std::unique_ptr<IClipboardMonitor> CreateClipboardMonitor();

} // namespace clipboard
