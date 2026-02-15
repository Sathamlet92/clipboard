#pragma once

#include "clipboard_monitor.h"
#include <wayland-client.h>
#include <atomic>
#include <thread>

// Forward declarations
struct zwlr_data_control_manager_v1;
struct zwlr_data_control_device_v1;
struct zwlr_data_control_source_v1;
struct zwlr_data_control_offer_v1;

namespace clipboard {

class WaylandMonitor : public IClipboardMonitor {
public:
    WaylandMonitor();
    ~WaylandMonitor() override;
    
    bool Initialize() override;
    void Run() override;
    void Stop() override;
    bool IsRunning() const override { return running_; }

private:
    void HandleSelection(zwlr_data_control_offer_v1* offer);
    ClipboardData ReadOfferData(zwlr_data_control_offer_v1* offer, const std::string& mime_type);
    
    wl_display* display_;
    wl_registry* registry_;
    wl_seat* seat_;
    zwlr_data_control_manager_v1* data_control_manager_;
    zwlr_data_control_device_v1* data_control_device_;
    zwlr_data_control_offer_v1* current_offer_;
    zwlr_data_control_offer_v1* pending_offer_;
    
    std::atomic<bool> running_;
    std::string current_mime_type_;
    std::vector<std::string> available_mime_types_;
    
    // Static callbacks for Wayland
    static void registry_global(void* data, wl_registry* registry,
                               uint32_t name, const char* interface,
                               uint32_t version);
    static void registry_global_remove(void* data, wl_registry* registry,
                                      uint32_t name);
    
    static void data_device_data_offer(void* data,
                                      zwlr_data_control_device_v1* device,
                                      zwlr_data_control_offer_v1* offer);
    static void data_device_selection(void* data,
                                     zwlr_data_control_device_v1* device,
                                     zwlr_data_control_offer_v1* offer);
    static void data_device_finished(void* data,
                                    zwlr_data_control_device_v1* device);
    static void data_device_primary_selection(void* data,
                                             zwlr_data_control_device_v1* device,
                                             zwlr_data_control_offer_v1* offer);
    
    static void data_offer_offer(void* data,
                                zwlr_data_control_offer_v1* offer,
                                const char* mime_type);
};

} // namespace clipboard
