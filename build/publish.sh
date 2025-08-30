#!/bin/bash

# FileServe200 - AOT Publishing Script
# This script builds a native self-contained executable for the file server

set -e  # Exit on any error

echo "🚀 Starting FileServe200 AOT build process..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Get script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo -e "${BLUE}📁 Project root: ${PROJECT_ROOT}${NC}"

# Default values
RUNTIME="linux-x64"
OUTPUT_DIR="${PROJECT_ROOT}/publish"
CONFIGURATION="Release"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -r|--runtime)
            RUNTIME="$2"
            shift 2
            ;;
        -o|--output)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        -h|--help)
            echo "FileServe200 AOT Publishing Script"
            echo ""
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  -r, --runtime <RID>       Target runtime identifier (default: linux-x64)"
            echo "                           Examples: linux-x64, win-x64, osx-x64, linux-arm64"
            echo "  -o, --output <DIR>        Output directory (default: ./publish)"
            echo "  -c, --configuration <CFG> Build configuration (default: Release)"
            echo "  -h, --help               Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0                        # Build for Linux x64"
            echo "  $0 -r win-x64            # Build for Windows x64"
            echo "  $0 -r osx-x64 -o ./dist  # Build for macOS, output to ./dist"
            exit 0
            ;;
        *)
            echo -e "${RED}❌ Unknown option: $1${NC}"
            echo "Use -h or --help for usage information"
            exit 1
            ;;
    esac
done

echo -e "${YELLOW}⚙️  Build Configuration:${NC}"
echo -e "   Runtime: ${RUNTIME}"
echo -e "   Configuration: ${CONFIGURATION}"
echo -e "   Output: ${OUTPUT_DIR}"
echo ""

# Change to project root
cd "$PROJECT_ROOT"

# Clean previous builds
echo -e "${BLUE}🧹 Cleaning previous builds...${NC}"
if [ -d "$OUTPUT_DIR" ]; then
    rm -rf "$OUTPUT_DIR"
    echo -e "${GREEN}✅ Cleaned output directory${NC}"
fi

# Restore dependencies
echo -e "${BLUE}📦 Restoring NuGet packages...${NC}"
dotnet restore
echo -e "${GREEN}✅ Dependencies restored${NC}"

# Build and publish with AOT
echo -e "${BLUE}🔨 Building native AOT executable...${NC}"
dotnet publish \
    -c "$CONFIGURATION" \
    -r "$RUNTIME" \
    --self-contained \
    -p:PublishAot=true \
    -p:PublishSingleFile=false \
    -o "$OUTPUT_DIR"

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ Build completed successfully!${NC}"
else
    echo -e "${RED}❌ Build failed!${NC}"
    exit 1
fi

# Get executable name based on runtime
if [[ "$RUNTIME" == win-* ]]; then
    EXECUTABLE_NAME="fileServe200.exe"
else
    EXECUTABLE_NAME="fileServe200"
fi

EXECUTABLE_PATH="${OUTPUT_DIR}/${EXECUTABLE_NAME}"

# Check if executable exists and get size
if [ -f "$EXECUTABLE_PATH" ]; then
    FILE_SIZE=$(du -h "$EXECUTABLE_PATH" | cut -f1)
    echo ""
    echo -e "${GREEN}🎉 Success! Native executable created:${NC}"
    echo -e "   📍 Location: ${EXECUTABLE_PATH}"
    echo -e "   📏 Size: ${FILE_SIZE}"
    echo -e "   🎯 Target: ${RUNTIME}"
    echo ""
    
    # Show file permissions (Unix-like systems)
    if [[ "$RUNTIME" != win-* ]]; then
        ls -la "$EXECUTABLE_PATH"
        echo ""
    fi
    
    echo -e "${BLUE}🚀 To run the server:${NC}"
    if [[ "$RUNTIME" == win-* ]]; then
        echo -e "   ${EXECUTABLE_PATH}"
    else
        echo -e "   ${EXECUTABLE_PATH}"
        echo -e "   # Or with custom port:"
        echo -e "   ${EXECUTABLE_PATH} --Port=8088"
    fi
    echo ""
    echo -e "${YELLOW}📋 Available endpoints:${NC}"
    echo -e "   🏠 Health check: http://localhost:8088/"
    echo -e "   📂 File browser: http://localhost:8088/browse/"
    echo -e "   📁 Static files: http://localhost:8088/files/"
    echo -e "   📤 File upload: http://localhost:8088/upload (POST)"
else
    echo -e "${RED}❌ Executable not found at expected location: ${EXECUTABLE_PATH}${NC}"
    exit 1
fi

echo -e "${GREEN}🎊 Build process completed successfully!${NC}"