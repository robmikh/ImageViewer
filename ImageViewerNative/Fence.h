#pragma once

template <typename FenceT, typename ContextT>
class Fence
{
public:
	Fence(winrt::com_ptr<FenceT> const& fence, winrt::com_ptr<ContextT> const& context)
	{
		m_fence = fence;
		m_context = context;
		m_event.reset(winrt::check_pointer(CreateEventExW(nullptr, nullptr, 0, EVENT_MODIFY_STATE | SYNCHRONIZE)));
	}

	void WaitForGpu()
	{
		uint64_t fenceValue = m_value;
		winrt::check_hresult(m_context->Signal(m_fence.get(), fenceValue));
		winrt::check_hresult(m_fence->SetEventOnCompletion(fenceValue, m_event.get()));
		m_event.wait();
		m_value++;
	}

private:
	winrt::com_ptr<FenceT> m_fence;
	winrt::com_ptr<ContextT> m_context;
	wil::unique_event m_event{ nullptr };
	uint64_t m_value = 0;
};

//using D3D12Fence = Fence<ID3D12Fence, ID3D12CommandQueue>;
using D3D11Fence = Fence<ID3D11Fence, ID3D11DeviceContext4>;

//inline std::shared_ptr<D3D12Fence> CreateD3D12Fence(winrt::com_ptr<ID3D12Device> const& d3d12Device, winrt::com_ptr<ID3D12CommandQueue> const& d3d12CommandQueue)
//{
//	winrt::com_ptr<ID3D12Fence> fence;
//	winrt::check_hresult(d3d12Device->CreateFence(0, D3D12_FENCE_FLAG_NONE, winrt::guid_of<ID3D12Fence>(), fence.put_void()));
//	return std::make_shared<D3D12Fence>(fence, d3d12CommandQueue);
//}

inline std::shared_ptr<D3D11Fence> CreateD3D11Fence(winrt::com_ptr<ID3D11Device5> const& d3d11Device, winrt::com_ptr<ID3D11DeviceContext4> const& d3d11Context)
{
	winrt::com_ptr<ID3D11Fence> fence;
	winrt::check_hresult(d3d11Device->CreateFence(0, D3D11_FENCE_FLAG_NONE, winrt::guid_of<ID3D11Fence>(), fence.put_void()));
	return std::make_shared<D3D11Fence>(fence, d3d11Context);
}
