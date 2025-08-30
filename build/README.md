# Build Scripts

This directory contains scripts for building and publishing the FileServe200 application.

## publish.sh

A comprehensive script for building native AOT (Ahead-of-Time) compiled executables of the FileServe200 file server.

### Features

- ✅ **Cross-platform builds** - Support for Linux, Windows, and macOS
- ✅ **Native AOT compilation** - Creates self-contained executables without .NET runtime dependency
- ✅ **Configurable options** - Runtime target, output directory, build configuration
- ✅ **Colored output** - Easy-to-read build progress and results
- ✅ **Error handling** - Stops on first error with clear messages
- ✅ **Size reporting** - Shows final executable size and location

### Usage

```bash
# Build for current platform (Linux x64)
./build/publish.sh

# Build for Windows x64
./build/publish.sh -r win-x64

# Build for macOS x64 with custom output directory
./build/publish.sh -r osx-x64 -o ./dist

# Build debug version
./build/publish.sh -c Debug

# Show help
./build/publish.sh --help
```

### Supported Runtime Identifiers

| Platform | Architecture | Runtime ID |
|----------|-------------|------------|
| Linux | x64 | `linux-x64` |
| Linux | ARM64 | `linux-arm64` |
| Windows | x64 | `win-x64` |
| Windows | x86 | `win-x86` |
| Windows | ARM64 | `win-arm64` |
| macOS | x64 | `osx-x64` |
| macOS | ARM64 (M1/M2) | `osx-arm64` |

### Output

The script creates a native executable in the specified output directory (default: `./publish/`). 

**Typical output sizes:**
- Linux x64: ~17MB
- Windows x64: ~18MB  
- macOS x64: ~17MB

### Requirements

- .NET 10 SDK
- Target platform toolchain (for cross-compilation)

### Example Build Output

```
🚀 Starting FileServe200 AOT build process...
📁 Project root: /home/user/fileServe200
⚙️  Build Configuration:
   Runtime: linux-x64
   Configuration: Release
   Output: ./publish

🧹 Cleaning previous builds...
✅ Cleaned output directory
📦 Restoring NuGet packages...
✅ Dependencies restored
🔨 Building native AOT executable...
✅ Build completed successfully!

🎉 Success! Native executable created:
   📍 Location: ./publish/fileServe200
   📏 Size: 17M
   🎯 Target: linux-x64

🚀 To run the server:
   ./publish/fileServe200
   # Or with custom port:
   ./publish/fileServe200 --Port=8088

📋 Available endpoints:
   🏠 Health check: http://localhost:8088/
   📂 File browser: http://localhost:8088/browse/
   📁 Static files: http://localhost:8088/files/
   📤 File upload: http://localhost:8088/upload (POST)

🎊 Build process completed successfully!
```