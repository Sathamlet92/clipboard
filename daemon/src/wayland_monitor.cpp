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
    display_ = wl_display_connect(nullptr);
    if (!display_) {
        std::cerr << "Failed to connect to Wayland display" << std::endl;
        return false;
    }
    
    registry_ = wl_display_get_registry(display_);
    if (!registry_) {
        std::cerr << "Failed to get Wayland registry" << std::endl;
        return false;
    }
    
    static const wl_registry_listener registry_listener = {
        .global = registry_global,
        .global_remove = registry_global_remove
    };
    
    wl_registry_add_listener(registry_, &registry_listener, this);
    wl_display_roundtrip(display_);
    
    if (!seat_ || !data_control_manager_) {
        std::cerr << "Required Wayland protocols not available" << std::endl;
        return false;
    }
    
    data_control_device_ = zwlr_data_control_manager_v1_get_data_device(
        data_control_manager_, seat_);
    
    if (!data_control_device_) {
        std::cerr << "Failed to create data control device" << std::endl;
        return false;
    }
    
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
        return;
    }
    
    running_ = true;
    std::cout << "Wayland monitor started" << std::endl;
    
    int fd = wl_display_get_fd(display_);
    
    while (running_) {
        while (wl_display_prepare_read(display_) != 0) {
            wl_display_dispatch_pending(display_);
        }
        
        wl_display_flush(display_);
        
        struct pollfd pfd = {
            .fd = fd,
            .events = POLLIN,
            .revents = 0
        };
        
        int ret = poll(&pfd, 1, 100);
        
        if (ret > 0) {
            wl_display_read_events(display_);
            wl_display_dispatch_pending(display_);
        } else if (ret == 0) {
            wl_display_cancel_read(display_);
        } else {
            wl_display_cancel_read(display_);
            if (errno != EINTR) {
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
    
    std::string mime_type = current_mime_type_.empty() ? "text/plain" : current_mime_type_;
    
    try {
        auto data = ReadOfferData(offer, mime_type);
        if (OnClipboardChanged) {
            OnClipboardChanged(data);
        }
    } catch (const std::exception& e) {
        std::cerr << "Error reading clipboard: " << e.what() << std::endl;
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
    
    int pipe_fds[2];
    if (pipe(pipe_fds) == -1) {
        throw std::runtime_error("Failed to create pipe");
    }
    
    int flags = fcntl(pipe_fds[0], F_GETFL, 0);
    fcntl(pipe_fds[0], F_SETFL, flags | O_NONBLOCK);
    
    zwlr_data_control_offer_v1_receive(offer, mime_type.c_str(), pipe_fds[1]);
    close(pipe_fds[1]);
    
    wl_display_flush(display_);
    usleep(5000);
    
    char buffer[4096];
    ssize_t bytes_read;
    size_t total_bytes = 0;
    
    for (int attempts = 0; attempts < 10; attempts++) {
        while ((bytes_read = read(pipe_fds[0], buffer, sizeof(buffer))) > 0) {
            result.data.insert(result.data.end(), buffer, buffer + bytes_read);
            total_bytes += bytes_read;
        }
        
        if (total_bytes > 0 || bytes_read == 0) {
            break;
        }
        
        wl_display_dispatch_pending(display_);
        usleep(5000);
    }
    
    close(pipe_fds[0]);
    
    if (mime_type.find("text/") == 0) {
        result.content_type = ContentType::TEXT;
    } else if (mime_type.find("image/") == 0) {
        result.content_type = ContentType::IMAGE;
    } else {
        result.content_type = ContentType::UNKNOWN;
    }
    
    return result;
}

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
}

void WaylandMonitor::data_device_data_offer(
    void* data,
    [[maybe_unused]] zwlr_data_control_device_v1* device,
    zwlr_data_control_offer_v1* offer)
{
    auto* monitor = static_cast<WaylandMonitor*>(data);
    monitor->current_mime_type_.clear();
    monitor->available_mime_types_.clear();
    
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
    
    if (offer) {
        monitor->HandleSelection(offer);
    }
}

void WaylandMonitor::data_device_finished(
    [[maybe_unused]] void* data,
    [[maybe_unused]] zwlr_data_control_device_v1* device)
{
}

void WaylandMonitor::data_device_primary_selection(
    [[maybe_unused]] void* data,
    [[maybe_unused]] zwlr_data_control_device_v1* device,
    [[maybe_unused]] zwlr_data_control_offer_v1* offer)
{
}

void WaylandMonitor::data_offer_offer(
    void* data,
    [[maybe_unused]] zwlr_data_control_offer_v1* offer,
    const char* mime_type)
{
    auto* monitor = static_cast<WaylandMonitor*>(data);
    std::string mime_str(mime_type);
    
    if (monitor->current_mime_type_.find("image/") == 0) {
        return;
    }
    
    if (mime_str.find("image/") == 0) {
        monitor->current_mime_type_ = mime_type;
    } else if (mime_str == "text/plain" || mime_str == "text/plain;charset=utf-8") {
        if (monitor->current_mime_type_.empty()) {
            monitor->current_mime_type_ = mime_type;
        }
    } else if (monitor->current_mime_type_.empty()) {
        monitor->current_mime_type_ = mime_type;
    }
}

} // namespace clipboard
