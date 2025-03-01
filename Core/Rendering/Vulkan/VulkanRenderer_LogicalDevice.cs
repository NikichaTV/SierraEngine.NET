using System.Runtime.InteropServices;
using Evergine.Bindings.Vulkan;

namespace SierraEngine.Core.Rendering.Vulkan;

public unsafe partial class VulkanRenderer
{
    private VkDevice logicalDevice;
    private VkQueue presentationQueue;
    private VkQueue graphicsQueue;
    
    private void CreateLogicalDevice()
    {
        // Filter out repeating indices using a HashSet
        HashSet<uint> uniqueQueueFamilies = new HashSet<uint>(2) { queueFamilyIndices.graphicsFamily!.Value, queueFamilyIndices.presentFamily!.Value };
        
        // Create an empty list to store the create infos
        List<VkDeviceQueueCreateInfo> queueCreateInfos = new List<VkDeviceQueueCreateInfo>(uniqueQueueFamilies.Count);

        // For each unique family create new create info and add it to the list
        float queuePriority = 1.0f;
        foreach (uint queueFamily in uniqueQueueFamilies)
        {
            VkDeviceQueueCreateInfo queueCreateInfo = new VkDeviceQueueCreateInfo()
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
                queueFamilyIndex = queueFamily,
                queueCount = 1,
                pQueuePriorities = &queuePriority,
            };
            queueCreateInfos.Add(queueCreateInfo);
        }

        // List required physical device features
        VkPhysicalDeviceFeatures requiredPhysicalDeviceFeatures = default;
        if (this.renderingMode != RenderingMode.Fill)
        {
            requiredPhysicalDeviceFeatures.fillModeNonSolid = VkBool32.True;
        }

        if (VulkanCore.physicalDeviceFeatures.samplerAnisotropy)
        {
            requiredPhysicalDeviceFeatures.samplerAnisotropy = VkBool32.True;
        }

        if (this.msaaSampleCount != VkSampleCountFlags.VK_SAMPLE_COUNT_1_BIT && this.sampleShadingEnabled)
        {
            requiredPhysicalDeviceFeatures.sampleRateShading = VkBool32.True;
        }
        
        // Convert to pointers and put every device extension into an array  
        IntPtr* deviceExtensionsArray = stackalloc IntPtr[requiredDeviceExtensions.Count];
        for (int i = 0; i < requiredDeviceExtensions.Count; i++)
        {
            deviceExtensionsArray[i] = Marshal.StringToHGlobalAnsi(requiredDeviceExtensions[i]);
        }
        
        VkPhysicalDeviceHostQueryResetFeatures resetFeatures = new VkPhysicalDeviceHostQueryResetFeatures()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_HOST_QUERY_RESET_FEATURES,
            hostQueryReset = VkBool32.True
        };
        
        // Fill in logical device creation info
        VkDeviceCreateInfo logicalDeviceCreateInfo = new VkDeviceCreateInfo()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
            pEnabledFeatures = &requiredPhysicalDeviceFeatures,
            enabledExtensionCount = (uint) requiredDeviceExtensions.Count,
            ppEnabledExtensionNames = (byte**) deviceExtensionsArray,
            pNext = &resetFeatures
        };
        
        // Reference queues create infos to the actual device create info
        VkDeviceQueueCreateInfo[] queueCreateInfosArray = queueCreateInfos.ToArray();
        fixed (VkDeviceQueueCreateInfo* queueCreateInfosArrayPtr = &queueCreateInfosArray[0])
        {
            logicalDeviceCreateInfo.queueCreateInfoCount = (uint)queueCreateInfos.Count;
            logicalDeviceCreateInfo.pQueueCreateInfos = queueCreateInfosArrayPtr;
        }

        // Create logical device
        fixed (VkDevice* logicalDevicePtr = &logicalDevice)
        {
            if (VulkanNative.vkCreateDevice(this.physicalDevice, &logicalDeviceCreateInfo, null, logicalDevicePtr) != VkResult.VK_SUCCESS)
            {
                VulkanDebugger.ThrowError("Failed to create logical device");
            }
        }
        
        // Assign the EngineCore's logical device
        VulkanCore.logicalDevice = logicalDevice;

        // Assign the EngineCore's graphics family index
        VulkanCore.graphicsFamilyIndex = queueFamilyIndices.graphicsFamily.Value;
        
        // Retrieve graphics queue
        fixed (VkQueue* graphicsQueuePtr = &graphicsQueue)
        {
            VulkanNative.vkGetDeviceQueue(logicalDevice, queueFamilyIndices.graphicsFamily.Value, 0, graphicsQueuePtr);
        }
        
        // Assign the EngineCore's graphicsQueue
        VulkanCore.graphicsQueue = graphicsQueue;

        // Retrieve presentation queue
        fixed(VkQueue* presentQueuePtr = &presentationQueue)
        {
            VulkanNative.vkGetDeviceQueue(logicalDevice, queueFamilyIndices.presentFamily.Value, 0, presentQueuePtr); 
        }
    }
}