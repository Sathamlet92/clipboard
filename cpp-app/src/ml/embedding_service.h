#pragma once

#include <string>
#include <vector>
#include <onnxruntime_cxx_api.h>
#include <memory>

class EmbeddingService {
public:
    explicit EmbeddingService(const std::string& model_path);
    ~EmbeddingService() = default;
    
    std::vector<float> generate_embedding(const std::string& text);
    bool is_available() const { return session_ != nullptr; }
    
private:
    std::unique_ptr<Ort::Env> env_;
    std::unique_ptr<Ort::Session> session_;
    std::unique_ptr<Ort::SessionOptions> session_options_;
    
    std::vector<int64_t> tokenize(const std::string& text);
    std::vector<float> mean_pooling(const std::vector<float>& token_embeddings, size_t seq_len, size_t hidden_size);
};
