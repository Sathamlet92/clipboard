#pragma once

#include <string>
#include <functional>
#include <memory>
#include <grpcpp/grpcpp.h>
#include "clipboard.grpc.pb.h"
#include "../services/clipboard_service.h"

class DaemonClient {
public:
    explicit DaemonClient(const std::string& server_address);
    
    void set_callback(std::function<void(const ClipboardEvent&)> callback);
    void start();
    void stop();
    
private:
    std::string server_address_;
    std::unique_ptr<clipboardmanager::ClipboardService::Stub> stub_;
    std::function<void(const ClipboardEvent&)> callback_;
    bool running_;
};
