#include "embedding_service.h"
#include <iostream>
#include <algorithm>
#include <numeric>

EmbeddingService::EmbeddingService(const std::string& model_path) {
    env_ = std::make_unique<Ort::Env>(ORT_LOGGING_LEVEL_WARNING, "EmbeddingService");
    session_options_ = std::make_unique<Ort::SessionOptions>();
    session_options_->SetIntraOpNumThreads(4);
    session_options_->SetGraphOptimizationLevel(GraphOptimizationLevel::ORT_ENABLE_ALL);
    
    session_ = std::make_unique<Ort::Session>(*env_, model_path.c_str(), *session_options_);
    
    std::cout << "✅ Embedding model loaded" << std::endl;
}

std::vector<float> EmbeddingService::generate_embedding(const std::string& text) {
    if (!session_) {
        return {};
    }
    
    try {
        // Tokenize
        auto tokens = tokenize(text);
        
        // Prepare inputs
        std::vector<int64_t> input_shape = {1, static_cast<int64_t>(tokens.size())};
        auto memory_info = Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);
        Ort::Value input_ids_tensor = Ort::Value::CreateTensor<int64_t>(
            memory_info, tokens.data(), tokens.size(), input_shape.data(), input_shape.size());

        std::vector<int64_t> attention_mask(tokens.size(), 0);
        for (size_t i = 0; i < tokens.size(); i++) {
            attention_mask[i] = tokens[i] == 0 ? 0 : 1;
        }
        Ort::Value attention_mask_tensor = Ort::Value::CreateTensor<int64_t>(
            memory_info, attention_mask.data(), attention_mask.size(),
            input_shape.data(), input_shape.size());

        std::vector<int64_t> token_type_ids(tokens.size(), 0);
        Ort::Value token_type_ids_tensor = Ort::Value::CreateTensor<int64_t>(
            memory_info, token_type_ids.data(), token_type_ids.size(),
            input_shape.data(), input_shape.size());

        Ort::AllocatorWithDefaultOptions allocator;
        size_t input_count = session_->GetInputCount();
        std::vector<std::string> input_names_storage;
        std::vector<const char*> input_names;
        std::vector<Ort::Value> input_tensors;
        input_names_storage.reserve(input_count);
        input_names.reserve(input_count);
        input_tensors.reserve(input_count);

        for (size_t i = 0; i < input_count; i++) {
            auto name_ptr = session_->GetInputNameAllocated(i, allocator);
            std::string name = name_ptr.get();
            input_names_storage.push_back(name);
            input_names.push_back(input_names_storage.back().c_str());

            if (name == "input_ids") {
                input_tensors.emplace_back(std::move(input_ids_tensor));
            } else if (name == "attention_mask") {
                input_tensors.emplace_back(std::move(attention_mask_tensor));
            } else if (name == "token_type_ids") {
                input_tensors.emplace_back(std::move(token_type_ids_tensor));
            } else {
                throw std::runtime_error("Unknown model input: " + name);
            }
        }

        auto out_name_ptr = session_->GetOutputNameAllocated(0, allocator);
        std::string output_name = out_name_ptr.get();
        const char* output_names[] = {output_name.c_str()};

        auto output_tensors = session_->Run(
            Ort::RunOptions{nullptr}, input_names.data(), input_tensors.data(), input_tensors.size(),
            output_names, 1);
        
        // Get output
        float* output_data = output_tensors[0].GetTensorMutableData<float>();
        auto output_shape = output_tensors[0].GetTensorTypeAndShapeInfo().GetShape();
        
        size_t seq_len = output_shape[1];
        size_t hidden_size = output_shape[2];
        
        std::vector<float> token_embeddings(output_data, output_data + (seq_len * hidden_size));
        
        // Mean pooling
        return mean_pooling(token_embeddings, seq_len, hidden_size);
        
    } catch (const std::exception& e) {
        std::cerr << "❌ Embedding generation failed: " << e.what() << std::endl;
        return {};
    }
}

std::vector<int64_t> EmbeddingService::tokenize(const std::string& text) {
    // Simple whitespace tokenization (in production, use proper tokenizer)
    std::vector<int64_t> tokens;
    tokens.push_back(101); // [CLS]
    
    // Add word tokens (simplified)
    for (char c : text) {
        if (c >= 'a' && c <= 'z') {
            tokens.push_back(c - 'a' + 1000);
        } else if (c >= 'A' && c <= 'Z') {
            tokens.push_back(c - 'A' + 1000);
        }
    }
    
    tokens.push_back(102); // [SEP]
    
    // Pad or truncate to 128
    if (tokens.size() > 128) {
        tokens.resize(128);
    } else {
        while (tokens.size() < 128) {
            tokens.push_back(0); // [PAD]
        }
    }
    
    return tokens;
}

std::vector<float> EmbeddingService::mean_pooling(
    const std::vector<float>& token_embeddings, size_t seq_len, size_t hidden_size) {
    
    std::vector<float> result(hidden_size, 0.0f);
    
    for (size_t i = 0; i < seq_len; ++i) {
        for (size_t j = 0; j < hidden_size; ++j) {
            result[j] += token_embeddings[i * hidden_size + j];
        }
    }
    
    for (float& val : result) {
        val /= seq_len;
    }
    
    return result;
}
