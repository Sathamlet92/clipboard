#pragma once

#include "clipboard_monitor.h"
#include "clipboard.grpc.pb.h"
#include <grpcpp/grpcpp.h>
#include <memory>
#include <queue>
#include <mutex>
#include <condition_variable>

namespace clipboard {

class ClipboardServiceImpl final : public clipboardmanager::ClipboardService::Service {
public:
    ClipboardServiceImpl(IClipboardMonitor* monitor);
    
    grpc::Status StreamClipboardEvents(
        grpc::ServerContext* context,
        const clipboardmanager::Empty* request,
        grpc::ServerWriter<clipboardmanager::ClipboardEvent>* writer) override;
    
    grpc::Status GetClipboardContent(
        grpc::ServerContext* context,
        const clipboardmanager::Empty* request,
        clipboardmanager::ClipboardContent* response) override;
    
    void OnClipboardChanged(const ClipboardData& data);

private:
    IClipboardMonitor* monitor_;
    std::queue<ClipboardData> event_queue_;
    std::mutex queue_mutex_;
    std::condition_variable queue_cv_;
    
    clipboardmanager::ClipboardEvent ConvertToProto(const ClipboardData& data);
};

class GrpcServer {
public:
    GrpcServer(const std::string& server_address, IClipboardMonitor* monitor);
    ~GrpcServer();
    
    void Run();
    void Shutdown();
    
    ClipboardServiceImpl* GetService() { return service_.get(); }

private:
    std::unique_ptr<grpc::Server> server_;
    std::unique_ptr<ClipboardServiceImpl> service_;
};

} // namespace clipboard
