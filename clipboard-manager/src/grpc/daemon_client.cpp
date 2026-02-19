#include "daemon_client.h"
#include <iostream>
#include <thread>
#include <chrono>

DaemonClient::DaemonClient(const std::string& server_address)
    : server_address_(server_address)
    , running_(false)
{
    auto channel = grpc::CreateChannel(server_address_, grpc::InsecureChannelCredentials());
    stub_ = clipboardmanager::ClipboardService::NewStub(channel);
}

void DaemonClient::set_callback(std::function<void(const ClipboardEvent&)> callback) {
    callback_ = callback;
}

void DaemonClient::start() {
    running_ = true;
    
    while (running_) {
        try {
            std::cout << "🔧 Attempting to connect to daemon at " << server_address_ << "..." << std::endl;
            grpc::ClientContext context;
            clipboardmanager::Empty request;
            clipboardmanager::ClipboardEvent response;
            
            auto reader = stub_->StreamClipboardEvents(&context, request);
            
            std::cout << "🔗 Connected to daemon, waiting for clipboard events..." << std::endl;
            
            while (reader->Read(&response)) {
                if (callback_) {
                    ClipboardEvent event;
                    
                    // Convert proto ContentType to string
                    switch (response.content_type()) {
                        case clipboardmanager::ContentType::TEXT:
                            event.content_type = "text";
                            event.text_content = std::string(response.data().begin(), response.data().end());
                            break;
                        case clipboardmanager::ContentType::IMAGE:
                            event.content_type = "image";
                            event.image_data = std::vector<uint8_t>(response.data().begin(), response.data().end());
                            break;
                        case clipboardmanager::ContentType::HTML:
                            event.content_type = "html";
                            event.text_content = std::string(response.data().begin(), response.data().end());
                            break;
                        case clipboardmanager::ContentType::FILE:
                            event.content_type = "file";
                            event.text_content = std::string(response.data().begin(), response.data().end());
                            break;
                        default:
                            event.content_type = "unknown";
                            break;
                    }
                    
                    event.timestamp = response.timestamp();
                    
                    std::cout << "📋 Received clipboard event: " << event.content_type << std::endl;
                    callback_(event);
                }
            }
            
            auto status = reader->Finish();
            if (!status.ok()) {
                std::cerr << "⚠️  Daemon disconnected: " << status.error_message() 
                          << " (code: " << status.error_code() << "), retrying in 5s..." << std::endl;
                std::this_thread::sleep_for(std::chrono::seconds(5));
            } else {
                std::cout << "ℹ️  Stream ended normally, retrying..." << std::endl;
                std::this_thread::sleep_for(std::chrono::seconds(2));
            }
        } catch (const std::exception& e) {
            std::cerr << "⚠️  Daemon error: " << e.what() << std::endl;
            std::this_thread::sleep_for(std::chrono::seconds(5));
        }
    }
}

void DaemonClient::stop() {
    running_ = false;
}
