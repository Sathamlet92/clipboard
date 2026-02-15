#include "grpc_server.h"
#include <iostream>

namespace clipboard {

ClipboardServiceImpl::ClipboardServiceImpl(IClipboardMonitor* monitor)
    : monitor_(monitor)
{
}

grpc::Status ClipboardServiceImpl::StreamClipboardEvents(
    grpc::ServerContext* context,
    [[maybe_unused]] const clipboardmanager::Empty* request,
    grpc::ServerWriter<clipboardmanager::ClipboardEvent>* writer)
{
    std::cout << "Client connected for clipboard events stream" << std::endl;
    
    while (!context->IsCancelled()) {
        std::unique_lock<std::mutex> lock(queue_mutex_);
        
        // Wait for events or timeout
        queue_cv_.wait_for(lock, std::chrono::seconds(1), [this] {
            return !event_queue_.empty();
        });
        
        // Process all queued events
        while (!event_queue_.empty()) {
            auto data = event_queue_.front();
            event_queue_.pop();
            
            auto event = ConvertToProto(data);
            
            if (!writer->Write(event)) {
                std::cout << "Client disconnected" << std::endl;
                return grpc::Status::OK;
            }
        }
    }
    
    std::cout << "Stream ended" << std::endl;
    return grpc::Status::OK;
}

grpc::Status ClipboardServiceImpl::GetClipboardContent(
    [[maybe_unused]] grpc::ServerContext* context,
    [[maybe_unused]] const clipboardmanager::Empty* request,
    [[maybe_unused]] clipboardmanager::ClipboardContent* response)
{
    // TODO: Implement getting current clipboard content
    return grpc::Status(grpc::StatusCode::UNIMPLEMENTED, "Not implemented yet");
}

void ClipboardServiceImpl::OnClipboardChanged(const ClipboardData& data) {
    std::lock_guard<std::mutex> lock(queue_mutex_);
    event_queue_.push(data);
    queue_cv_.notify_all();
}

clipboardmanager::ClipboardEvent ClipboardServiceImpl::ConvertToProto(const ClipboardData& data) {
    clipboardmanager::ClipboardEvent event;
    
    event.set_data(data.data.data(), data.data.size());
    event.set_source_app(data.source_app);
    event.set_window_title(data.window_title);
    event.set_timestamp(data.timestamp);
    event.set_mime_type(data.mime_type);
    
    // Convert content type
    switch (data.content_type) {
        case ContentType::TEXT:
            event.set_content_type(clipboardmanager::ContentType::TEXT);
            break;
        case ContentType::IMAGE:
            event.set_content_type(clipboardmanager::ContentType::IMAGE);
            break;
        case ContentType::HTML:
            event.set_content_type(clipboardmanager::ContentType::HTML);
            break;
        case ContentType::FILE:
            event.set_content_type(clipboardmanager::ContentType::FILE);
            break;
        default:
            event.set_content_type(clipboardmanager::ContentType::UNKNOWN);
            break;
    }
    
    return event;
}

// GrpcServer implementation

GrpcServer::GrpcServer(const std::string& server_address, IClipboardMonitor* monitor)
{
    service_ = std::make_unique<ClipboardServiceImpl>(monitor);
    
    grpc::ServerBuilder builder;
    builder.AddListeningPort(server_address, grpc::InsecureServerCredentials());
    builder.RegisterService(service_.get());
    
    server_ = builder.BuildAndStart();
    
    if (server_) {
        std::cout << "gRPC server listening on " << server_address << std::endl;
    } else {
        std::cerr << "Failed to start gRPC server" << std::endl;
    }
}

GrpcServer::~GrpcServer() {
    Shutdown();
}

void GrpcServer::Run() {
    if (server_) {
        server_->Wait();
    }
}

void GrpcServer::Shutdown() {
    if (server_) {
        server_->Shutdown();
    }
}

} // namespace clipboard
