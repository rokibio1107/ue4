// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "HeadMountedDisplayBase.h"
#include "XRRenderBridge.h"
#include "XRSwapChain.h"
#include "OpenXRPlatformRHI.h"

#include <openxr/openxr.h>

class FOpenXRHMD;
class FRHICommandListImmediate;

class FOpenXRRenderBridge : public FXRRenderBridge
{
public:
	FOpenXRRenderBridge()
		: AdapterLuid(0)
		, OpenXRHMD(nullptr)
	{ }

	void SetOpenXRHMD(FOpenXRHMD* InHMD) { OpenXRHMD = InHMD; }
	virtual uint64 GetGraphicsAdapterLuid() { return AdapterLuid; }

	virtual void* GetGraphicsBinding() = 0;
	virtual FXRSwapChainPtr CreateSwapchain(XrSession InSession, uint8 Format, uint32 SizeX, uint32 SizeY, uint32 ArraySize, uint32 NumMips, uint32 NumSamples, ETextureCreateFlags Flags, ETextureCreateFlags TargetableTextureFlags, const FClearValueBinding& ClearValueBinding) = 0;

	FXRSwapChainPtr CreateSwapchain(XrSession InSession, FRHITexture2D* Template, ETextureCreateFlags Flags, ETextureCreateFlags TargetableTextureFlags)
	{
		if (!Template)
		{
			return nullptr;
		}

		return CreateSwapchain(InSession,
			Template->GetFormat(),
			Template->GetSizeX(),
			Template->GetSizeY(),
			1,
			Template->GetNumMips(),
			Template->GetNumSamples(),
			Template->GetFlags() | Flags,
			TargetableTextureFlags,
			Template->GetClearBinding());
	}

	/** FRHICustomPresent */
	virtual bool Present(int32& InOutSyncInterval) override;
	// Called from RenderThread
	virtual bool NeedsNativePresent() override
	{
		return bNativePresent_RenderThread;
	}


	virtual void ShouldNativePresent_RenderThread(bool bPresent)
	{
		bNativePresent_RenderThread = bPresent;
	}

	virtual void ShouldNativePresent_RHIThread(bool bPresent)
	{
		bNativePresent_RHIThread = bPresent;
	}

protected:
	uint64 AdapterLuid;
	bool bNativePresent_RenderThread = true;
	bool bNativePresent_RHIThread = true;
	
	FOpenXRHMD* OpenXRHMD;

private:
};

#ifdef XR_USE_GRAPHICS_API_D3D11
FOpenXRRenderBridge* CreateRenderBridge_D3D11(XrInstance InInstance, XrSystemId InSystem);
#endif
#ifdef XR_USE_GRAPHICS_API_D3D12
FOpenXRRenderBridge* CreateRenderBridge_D3D12(XrInstance InInstance, XrSystemId InSystem);
#endif
#ifdef XR_USE_GRAPHICS_API_OPENGL
FOpenXRRenderBridge* CreateRenderBridge_OpenGL(XrInstance InInstance, XrSystemId InSystem);
#endif
#ifdef XR_USE_GRAPHICS_API_VULKAN
FOpenXRRenderBridge* CreateRenderBridge_Vulkan(XrInstance InInstance, XrSystemId InSystem);
#endif
