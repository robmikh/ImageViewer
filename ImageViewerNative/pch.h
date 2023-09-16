#pragma once

// Windows SDK
#include <unknwn.h>
#include <inspectable.h>
#include <Windows.h>
#include <libloaderapi.h>

// WinRT
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Graphics.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <winrt/Windows.Storage.Streams.h>

// WinRT Interop
#include <robuffer.h>
#include <shcore.h>

// WIL
#include <wil/resource.h>

// DirectX
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <d2d1_3.h>
#include <wincodec.h>

// Media Foundation
#include <mfidl.h>
#include <mfapi.h>
#include <mfreadwrite.h>
#include <mferror.h>
#include <wmcodecdsp.h>
#include <codecapi.h>

// STL
#include <vector>
#include <string>
#include <atomic>
#include <memory>
#include <algorithm>
#include <mutex>
#include <sstream>

// robmikh.common
#include <robmikh.common/d3dHelpers.h>
#include <robmikh.common/direct3d11.interop.h>
#include <robmikh.common/mfHelpers.h>
