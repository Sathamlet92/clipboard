#include "wayland_monitor.h"
#include "wlr-data-control-unstable-v1-client-protocol.h"
#include <iostream>
#include <cstring>
#include <unistd.h>
#include <fcntl.h>
#include <poll.h>
#include <cerrno>
#include <chrono>

namespace clipboard {

WaylandMonitor::WaylandMonitor()
    : display_(nullptr)
    , registry_(nullptr)
    , seat_(nullptr)
    , data_control_manager_(nullptr)
    , data_control_device_(nullptr)
    , current_offer_(nullptr)
    , pending_offer_(nullptr)
    , running_(false)
{
}

WaylandMonitor::~WaylandMonitor() {
    Stop();
    
    if (data_control_device_) {
        zwlr_data_control_device_v1_destroy(data_control_device_);
    }
    if (data_control_manager_) {
        zwlr_data_control_manager_v1_destroy(data_control_manager_);
    }
    if (seat_) {
        wl_seat_destroy(seat_);
    }
    if (registry_) {
        wl_registry_destroy(registry_);
    }
    if (display_) {
        wl_display_disconnect(display_);
    }
}

bool WaylandMonitor::Initialize() {
    // Connect to Wayland display
    display_ = wl_display_connect(nullptr);
    if (!display_) {
        std::cerr << "Failed to connect to Wayland display" << std::endl;
        return false;
    }
    
    // Get registry
    registry_ = wl_display_get_registry(display_);
    if (!registry_) {
        std::cerr << "Failed to get Wayland registry" << std::endl;
        return false;
    }
    
    // Registry listener
    static const wl_registry_listener registry_listener = {
        .global = registry_global,
        .global_remove = registry_global_remove
    };
    
    wl_registry_add_listener(registry_, &registry_listener, this);
    
    // Roundtrip to get globals
    wl_display_roundtrip(display_);
    
    if (!seat_ || !data_control_manager_) {
        std::cerr << "Required Wayland protocols not available" << std::endl;
        std::cerr << "  wl_seat: " << (seat_ ? "found" : "missing") << std::endl;
        std::cerr << "  zwlr_data_control_manager_v1: " 
                  << (data_control_manager_ ? "found" : "missing") << std::endl;
        return false;
    }
    
    // Create data control device
    data_control_device_ = zwlr_data_control_manager_v1_get_data_device(
        data_control_manager_, seat_);
    
    if (!data_control_device_) {
        std::cerr << "Failed to create data control device" << std::endl;
        return false;
    }
    
    // Data device listener
    static const zwlr_data_control_device_v1_listener device_listener = {
        .data_offer = data_device_data_offer,
        .selection = data_device_selection,
        .finished = data_device_finished,
        .primary_selection = data_device_primary_selection
    };
    
    zwlr_data_control_device_v1_add_listener(
        data_control_device_, &device_listener, this);
    
    wl_display_roundtrip(display_);
    
    std::cout << "Wayland monitor initialized successfully" << std::endl;
    return true;
}

void WaylandMonitor::Run() {
    if (!display_) {
        std::cerr << "Monitor not initialized" << std::endl;
        return;
    }
    
    running_ = true;
    std::cout << "Wayland monitor started" << std::endl;
    
    int fd = wl_display_get_fd(display_);
    
    while (running_) {
        // Dispatch pending events first
        while (wl_display_prepare_read(display_) != 0) {
            wl_display_dispatch_pending(display_);
        }
        
        wl_display_flush(display_);
        
        // Poll with timeout so we can check running_ flag
        struct pollfd pfd = {
            .fd = fd,
            .events = POLLIN,
            .revents = 0
        };
        
        int ret = poll(&pfd, 1, 100); // 100ms timeout
        
        if (ret > 0) {
            wl_display_read_events(display_);
            wl_display_dispatch_pending(display_);
        } else if (ret == 0) {
            // Timeout, cancel read
            wl_display_cancel_read(display_);
        } else {
            // Error
            wl_display_cancel_read(display_);
            if (errno != EINTR) {
                std::cerr << "Poll error: " << strerror(errno) << std::endl;
                break;
            }
        }
    }
    
    std::cout << "Wayland monitor stopped" << std::endl;
}

void WaylandMonitor::Stop() {
    running_ = false;
}

void WaylandMonitor::HandleSelection(zwlr_data_control_offer_v1* offer) {
    if (!offer) {
        return;
    }
    
    std::cout << "📋 Clipboard changed (Wayland)" << std::endl;
    std::cout << "   Selected MIME type: " << current_mime_type_ << std::endl;
    
    // Ignorar si no hay MIME type válido
    if (current_mime_type_.empty()) {
        std::cout << "   ⚠️  No valid MIME type selected, ignoring" << std::endl;
        return;
    }
    
    // Ignorar MIME types de metadata
    if (current_mime_type_ == "SAVE_TARGETS" || 
        current_mime_type_ == "TARGETS" ||
        current_mime_type_ == "MULTIPLE" ||
        current_mime_type_ == "TIMESTAMP" ||
        current_mime_type_.find("chromium/") == 0) {
        std::cout << "   ⏭️  Ignoring metadata MIME type: " << current_mime_type_ << std::endl;
        return;
    }
    
    std::string mime_type = current_mime_type_;
    
    try {
        auto data = ReadOfferData(offer, mime_type);
        
        // Ignorar si no hay datos
        if (data.data.empty()) {
            std::cout << "   ⚠️  No data read, ignoring" << std::endl;
            return;
        }
        
        std::cout << "   ✅ Read " << data.data.size() << " bytes" << std::endl;
        
        if (OnClipboardChanged) {
            OnClipboardChanged(data);
        }
    } catch (const std::exception& e) {
        std::cerr << "   ❌ Error reading clipboard: " << e.what() << std::endl;
    }
}

ClipboardData WaylandMonitor::ReadOfferData(
    zwlr_data_control_offer_v1* offer,
    const std::string& mime_type)
{
    ClipboardData result;
    result.timestamp = std::chrono::duration_cast<std::chrono::seconds>(
        std::chrono::system_clock::now().time_since_epoch()
    ).count();
    result.source_app = "wayland";
    result.window_title = "wayland";
    result.mime_type = mime_type;
    
    std::cout << "  Reading data for MIME: " << mime_type << std::endl;
    
    // Create pipe
    int pipe_fds[2];
    if (pipe(pipe_fds) == -1) {
        throw std::runtime_error("Failed to create pipe");
    }
    
    // Make read end non-blocking
    int flags = fcntl(pipe_fds[0], F_GETFL, 0);
    fcntl(pipe_fds[0], F_SETFL, flags | O_NONBLOCK);
    
    // Request data
    zwlr_data_control_offer_v1_receive(offer, mime_type.c_str(), pipe_fds[1]);
    close(pipe_fds[1]);
    
    // Flush to send the request
    wl_display_flush(display_);
    
    // Wait a bit for data to arrive
    usleep(5000); // 5ms
    
    // Read data
    char buffer[4096];
    ssize_t bytes_read;
    size_t total_bytes = 0;
    
    // Try reading multiple times
    for (int attempts = 0; attempts < 10; attempts++) {
        while ((bytes_read = read(pipe_fds[0], buffer, sizeof(buffer))) > 0) {
            result.data.insert(result.data.end(), buffer, buffer + bytes_read);
            total_bytes += bytes_read;
        }
        
        if (total_bytes > 0 || bytes_read == 0) {
            break; // Got data or EOF
        }
        
        // Dispatch events to process the transfer
        wl_display_dispatch_pending(display_);
        usleep(5000); // 5ms
    }
    
    close(pipe_fds[0]);
    
    std::cout << "  Read " << total_bytes << " bytes" << std::endl;
    
    // Determine content type
    if (mime_type.find("text/") == 0) {
        result.content_type = ContentType::TEXT;
    } else if (mime_type.find("image/") == 0) {
        result.content_type = ContentType::IMAGE;
    } else {
        result.content_type = ContentType::UNKNOWN;
    }
    
    return result;
}

// Static callbacks

void WaylandMonitor::registry_global(
    void* data,
    wl_registry* registry,
    uint32_t name,
    const char* interface,
    [[maybe_unused]] uint32_t version)
{
    auto* monitor = static_cast<WaylandMonitor*>(data);
    
    if (strcmp(interface, wl_seat_interface.name) == 0) {
        monitor->seat_ = static_cast<wl_seat*>(
            wl_registry_bind(registry, name, &wl_seat_interface, 1));
    } else if (strcmp(interface, zwlr_data_control_manager_v1_interface.name) == 0) {
        monitor->data_control_manager_ = static_cast<zwlr_data_control_manager_v1*>(
            wl_registry_bind(registry, name, &zwlr_data_control_manager_v1_interface, 2));
    }
}

void WaylandMonitor::registry_global_remove(
    [[maybe_unused]] void* data,
    [[maybe_unused]] wl_registry* registry,
    [[maybe_unused]] uint32_t name)
{
    // Not needed for now
}

void WaylandMonitor::data_device_data_offer(
    void* data,
    [[maybe_unused]] zwlr_data_control_device_v1* device,
    zwlr_data_control_offer_v1* offer)
{
    auto* monitor = static_cast<WaylandMonitor*>(data);
    
    std::cout << "🔔 New clipboard offer detected" << std::endl;
    
    // Reset MIME type for new offer
    monitor->current_mime_type_.clear();
    monitor->available_mime_types_.clear();
    
    // Data offer listener
    static const zwlr_data_control_offer_v1_listener offer_listener = {
        .offer = data_offer_offer
    };
    
    zwlr_data_control_offer_v1_add_listener(offer, &offer_listener, monitor);
}

void WaylandMonitor::data_device_selection(
    void* data,
    [[maybe_unused]] zwlr_data_control_device_v1* device,
    zwlr_data_control_offer_v1* offer)
{
    auto* monitor = static_cast<WaylandMonitor*>(data);
    monitor->current_offer_ = offer;
    
    // At this point, all MIME types have been offered
    // So current_mime_type_ should be set correctly
    
    if (offer) {
        monitor->HandleSelection(offer);
    }
}

void WaylandMonitor::data_device_finished(
    [[maybe_unused]] void* data,
    [[maybe_unused]] zwlr_data_control_device_v1* device)
{
    // Device finished, cleanup if needed
}

void WaylandMonitor::data_device_primary_selection(
    [[maybe_unused]] void* data,
    [[maybe_unused]] zwlr_data_control_device_v1* device,
    [[maybe_unused]] zwlr_data_control_offer_v1* offer)
{
    // Primary selection (middle-click paste), ignore for now
}

void WaylandMonitor::data_offer_offer(
    void* data,
    [[maybe_unused]] zwlr_data_control_offer_v1* offer,
    const char* mime_type)
{
    auto* monitor = static_cast<WaylandMonitor*>(data);
    
    std::string mime_str(mime_type);
    std::cout << "   📎 MIME offered: " << mime_str << std::endl;
    
    // Ignorar MIME types de metadata/control
    if (mime_str == "SAVE_TARGETS" || 
        mime_str == "TARGETS" ||
        mime_str == "MULTIPLE" ||
        mime_str == "TIMESTAMP" ||
        mime_str.find("chromium/") == 0) {
        std::cout << "      ⏭️  Skipping metadata" << std::endl;
        return; // Skip metadata
    }
    
    // Priority: image > text/plain > UTF8_STRING > other text > other
    // If we already have an image MIME type, keep it
    if (monitor->current_mime_type_.find("image/") == 0) {
        return; // Already have image, don't override
    }
    
    // Prefer image MIME types
    if (mime_str.find("image/") == 0) {
        monitor->current_mime_type_ = mime_type;
        return;
    }
    
    // text/plain is best for text
    if (mime_str == "text/plain" || mime_str == "text/plain;charset=utf-8") {
        monitor->current_mime_type_ = mime_type;
        return;
    }
    
    // UTF8_STRING is common in Linux apps
    if (mime_str == "UTF8_STRING" || mime_str == "STRING" || mime_str == "TEXT") {
        if (monitor->current_mime_type_.empty() || 
            monitor->current_mime_type_.find("text/") != 0) {
            monitor->current_mime_type_ = mime_type;
        }
        return;
    }
    
    // text/* variants
    if (mime_str.find("text/") == 0) {
        if (monitor->current_mime_type_.empty()) {
            monitor->current_mime_type_ = mime_type;
        }
        return;
    }
    
    // Store any MIME type if we don't have one yet (except metadata)
    if (monitor->current_mime_type_.empty()) {
        monitor->current_mime_type_ = mime_type;
    }
}

} // namespace clipboard
