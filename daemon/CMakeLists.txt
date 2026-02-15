cmake_minimum_required(VERSION 3.20)
project(clipboard-daemon VERSION 1.0.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# Find required packages
find_package(PkgConfig REQUIRED)

# Use pkg-config for protobuf and gRPC to avoid conflicts
pkg_check_modules(PROTOBUF REQUIRED protobuf)
pkg_check_modules(GRPC REQUIRED grpc++)
pkg_check_modules(GRPCPP REQUIRED grpc++)

# X11 libraries
pkg_check_modules(X11 REQUIRED x11)
pkg_check_modules(XFIXES REQUIRED xfixes)

# Wayland libraries (optional)
pkg_check_modules(WAYLAND wayland-client)

# Check for wlr-data-control protocol
set(WLR_PROTOCOL_DIR "${CMAKE_CURRENT_SOURCE_DIR}/protocols")
set(WLR_PROTOCOL_HEADER "${WLR_PROTOCOL_DIR}/wlr-data-control-unstable-v1-client-protocol.h")
set(WLR_PROTOCOL_CODE "${WLR_PROTOCOL_DIR}/wlr-data-control-unstable-v1-protocol.c")

if(WAYLAND_FOUND AND EXISTS "${WLR_PROTOCOL_HEADER}")
    set(HAVE_WAYLAND TRUE)
    message(STATUS "Wayland support enabled with wlr-data-control")
else()
    set(HAVE_WAYLAND FALSE)
    if(WAYLAND_FOUND)
        message(STATUS "Wayland libraries found but wlr-data-control protocol missing")
        message(STATUS "Run: ./scripts/setup-wayland.sh to generate protocol bindings")
    else()
        message(STATUS "Wayland libraries not found")
    endif()
    message(STATUS "Wayland support disabled")
endif()

# Protobuf generation
set(PROTO_FILES "${CMAKE_CURRENT_SOURCE_DIR}/proto/clipboard.proto")
set(PROTO_OUTPUT_DIR "${CMAKE_CURRENT_BINARY_DIR}/generated")
file(MAKE_DIRECTORY ${PROTO_OUTPUT_DIR})

# Find protoc and grpc_cpp_plugin
find_program(PROTOC protoc REQUIRED)
find_program(GRPC_CPP_PLUGIN grpc_cpp_plugin REQUIRED)

# Generate protobuf and gRPC files
add_custom_command(
    OUTPUT 
        "${PROTO_OUTPUT_DIR}/clipboard.pb.cc"
        "${PROTO_OUTPUT_DIR}/clipboard.pb.h"
        "${PROTO_OUTPUT_DIR}/clipboard.grpc.pb.cc"
        "${PROTO_OUTPUT_DIR}/clipboard.grpc.pb.h"
    COMMAND ${PROTOC}
    ARGS 
        --grpc_out=${PROTO_OUTPUT_DIR}
        --cpp_out=${PROTO_OUTPUT_DIR}
        --plugin=protoc-gen-grpc=${GRPC_CPP_PLUGIN}
        -I ${CMAKE_CURRENT_SOURCE_DIR}/proto
        ${PROTO_FILES}
    DEPENDS ${PROTO_FILES}
    COMMENT "Generating protobuf and gRPC files"
)

# Source files
set(SOURCES
    src/main.cpp
    src/clipboard_monitor.cpp
    src/x11_monitor.cpp
    src/grpc_server.cpp
    ${PROTO_OUTPUT_DIR}/clipboard.pb.cc
    ${PROTO_OUTPUT_DIR}/clipboard.grpc.pb.cc
)

# Conditionally add Wayland support
if(HAVE_WAYLAND)
    list(APPEND SOURCES 
        src/wayland_monitor.cpp
        ${WLR_PROTOCOL_CODE}
    )
    add_compile_definitions(HAVE_WAYLAND)
    message(STATUS "Wayland support enabled")
    message(STATUS "  Protocol code: ${WLR_PROTOCOL_CODE}")
else()
    message(STATUS "Wayland support disabled (libraries not found)")
endif()

# Executable
add_executable(clipboard-daemon ${SOURCES})

# Add protocol C file separately if Wayland is enabled
if(HAVE_WAYLAND)
    target_sources(clipboard-daemon PRIVATE ${WLR_PROTOCOL_CODE})
    set_source_files_properties(${WLR_PROTOCOL_CODE} PROPERTIES LANGUAGE C)
endif()

# Include directories
target_include_directories(clipboard-daemon PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
    ${PROTO_OUTPUT_DIR}
    ${X11_INCLUDE_DIRS}
    ${XFIXES_INCLUDE_DIRS}
    ${PROTOBUF_INCLUDE_DIRS}
    ${GRPC_INCLUDE_DIRS}
)

if(HAVE_WAYLAND)
    target_include_directories(clipboard-daemon PRIVATE
        ${WAYLAND_INCLUDE_DIRS}
        ${WLR_PROTOCOL_DIR}
    )
endif()

# Link libraries
target_link_libraries(clipboard-daemon PRIVATE
    ${PROTOBUF_LIBRARIES}
    ${GRPC_LIBRARIES}
    ${GRPCPP_LIBRARIES}
    ${X11_LIBRARIES}
    ${XFIXES_LIBRARIES}
    pthread
)

if(HAVE_WAYLAND)
    target_link_libraries(clipboard-daemon PRIVATE
        ${WAYLAND_LIBRARIES}
    )
endif()

# Compiler warnings
target_compile_options(clipboard-daemon PRIVATE
    -Wall -Wextra -Wpedantic
    $<$<CONFIG:Debug>:-g -O0>
    $<$<CONFIG:Release>:-O3>
)

# Install
install(TARGETS clipboard-daemon DESTINATION bin)
