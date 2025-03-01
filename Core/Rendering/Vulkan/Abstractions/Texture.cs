using Evergine.Bindings.Vulkan;
using SierraEngine.Engine.Classes;
using StbImageSharp;

namespace SierraEngine.Core.Rendering.Vulkan.Abstractions;

public unsafe class Texture
{
    public class Builder
    {
        private string name = String.Empty; 
        
        private TextureType textureType = TextureType.None;
        private ColorComponents colors = ColorComponents.RedGreenBlueAlpha;
        
        private DescriptorSetLayout descriptorSetLayout = null!;
        private DescriptorPool descriptorPool = null!;
        
        private Sampler sampler = null!;
        private bool mipMappingEnabled = true;

        public Builder SetName(in string givenName)
        {
            // Save given name
            this.name = givenName;
            return this;
        }

        public Builder SetTextureType(in TextureType givenTextureType)
        {
            // Save given texture type
            this.textureType = givenTextureType;
            return this;
        }

        public Builder SetColors(in ColorComponents givenColors)
        {
            // Save given colors
            this.colors = givenColors;
            return this;
        }

        public Builder EnableMipMapGeneration(in bool enable)
        {
            // Save given mip generation toggle
            mipMappingEnabled = enable;
            return this;
        }

        public Builder SetSampler(in Sampler givenSampler)
        {
            // Save given sampler
            this.sampler = givenSampler;
            return this;
        }

        public Builder SetDescriptorSetLayout(in DescriptorSetLayout givenDescriptorSetLayout)
        {
            // Save given descriptor set layout
            this.descriptorSetLayout = givenDescriptorSetLayout;
            return this;
        }

        public Builder SetDescriptorPool(in DescriptorPool givenDescriptorPool)
        {
            // Save given descriptor pool
            this.descriptorPool = givenDescriptorPool;
            return this;
        }

        public void Build(in string filePath, out Texture texture)
        {
            // Set a default name if none is assigned
            if (name == String.Empty) name = filePath;
            
            // Load image file
            ImageResult loadedImage = ImageResult.FromMemory(Files.GetBytes(filePath), colors);
            
            // Create the texture
            texture = new Texture((uint) loadedImage.Width, (uint) loadedImage.Height, loadedImage.Data, textureType, colors, mipMappingEnabled, sampler, descriptorSetLayout, descriptorPool, name);
        }

        public void Build(in uint width, in uint height, in byte[] imageBytes, out Texture texture)
        {
            texture = new Texture(width, height, imageBytes, textureType, colors, mipMappingEnabled, sampler, descriptorSetLayout, descriptorPool, name);
        }
    }
    
    public string name;

    public readonly TextureType textureType;
    public readonly ColorComponents colors;

    public readonly Sampler sampler;
    public readonly uint mipMapLevels = 1;
    public readonly bool mipMappingEnabled;

    public readonly ulong memorySize;
    public readonly VkDescriptorSet descriptorSet;

    public readonly Image image;
    
    public uint width => image.width;
    public uint height => image.height;
    public ulong handle => descriptorSet.Handle;
    
    private Texture(
        uint width, uint height, byte[] imageBytes, TextureType givenTextureType, ColorComponents givenColors, bool givenMipMappingEnabled,
        Sampler givenSampler, DescriptorSetLayout descriptorSetLayout, DescriptorPool descriptorPool, string givenName)
    {
        // Save provided data
        this.name = givenName;
        this.textureType = givenTextureType;
        this.colors = givenColors;
        this.sampler = givenSampler;

        // If mip mapping is enabled calculate mip levels
        if (givenMipMappingEnabled)
        {
            this.mipMappingEnabled = true;
            mipMapLevels = (uint) Math.Floor(Math.Log2(Math.Max(width, height)) + 1);
        }
        
        // Calculate the image's memory size
        this.memorySize = (ulong) (width * height * (int) colors);

        Buffer stagingBuffer;
        
        // Create the staging buffer
        new Buffer.Builder()
            .SetMemorySize(memorySize)
            .SetMemoryFlags(VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_HOST_COHERENT_BIT)
            .SetUsageFlags(VkBufferUsageFlags.VK_BUFFER_USAGE_TRANSFER_SRC_BIT)
        .Build(out stagingBuffer);
        
        // Copy the image data to the staging buffer
        stagingBuffer.CopyBytes(imageBytes);
        
        // Configure the color format
        VkFormat textureImageFormat = colors switch
        {
            ColorComponents.RedGreenBlueAlpha => VkFormat.VK_FORMAT_R8G8B8A8_SRGB,
            ColorComponents.RedGreenBlue => VkFormat.VK_FORMAT_R8G8B8_SRGB,
            ColorComponents.GreyAlpha => VkFormat.VK_FORMAT_R8G8_SRGB,
            _ => VkFormat.VK_FORMAT_R8_SRGB
        };
        
        // Create the texture image
        new Image.Builder()
            .SetSize(width, height)
            .SetMipLevels(mipMapLevels)
            .SetFormat(textureImageFormat)
            .SetUsage(VkImageUsageFlags.VK_IMAGE_USAGE_TRANSFER_SRC_BIT | VkImageUsageFlags.VK_IMAGE_USAGE_TRANSFER_DST_BIT | VkImageUsageFlags.VK_IMAGE_USAGE_SAMPLED_BIT)
            .SetMemoryFlags(VkMemoryPropertyFlags.VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT)
        .Build(out image);
        
        // Transition the layout of the image, so it can be used for copying
        image.TransitionLayout(VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL);
        
        // Copy the image to the staging buffer
        stagingBuffer.CopyImage(image);


        // Destroy the staging buffer and free its memory
        stagingBuffer.CleanUp();
        
        // Generate mip maps for the current texture
        GenerateMipMaps();
        
        // NOTE: Transitioning to VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL is not required as it is automatically done during the mip map generation
        
        // Create the image view using the proper image format
        image.GenerateImageView(VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT);
        
        // Create the information on the image
        VkDescriptorImageInfo textureSamplerImageInfo = new VkDescriptorImageInfo()
        {
            imageLayout = VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
            imageView = image,
            sampler = sampler
        };

        // Write the image to the descriptor set
        new DescriptorWriter(descriptorSetLayout, descriptorPool)
            .WriteImage(TextureTypeToBinding(textureType), textureSamplerImageInfo)
        .Build(out descriptorSet);
    }

    private void GenerateMipMaps()
    {
        // Get the properties of the image's format
        VkFormatProperties formatProperties;
        VulkanNative.vkGetPhysicalDeviceFormatProperties(VulkanCore.physicalDevice, image.format, &formatProperties);

        // Check if optimal tiling is supported by the GPU
        if ((formatProperties.optimalTilingFeatures & VkFormatFeatureFlags.VK_FORMAT_FEATURE_SAMPLED_IMAGE_FILTER_LINEAR_BIT) == 0)
        {
            VulkanDebugger.ThrowError($"Texture image format [{ image.format.ToString() }] does not support linear blitting");
        }
        
        // Begin a command buffer
        VkCommandBuffer commandBuffer = VulkanUtilities.BeginSingleTimeCommands();

        // Create an image memory barrier
        VkImageMemoryBarrier memoryBarrier = new VkImageMemoryBarrier()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER,
            image = image,
            srcQueueFamilyIndex = ~0U,
            dstQueueFamilyIndex = ~0U,
            subresourceRange = new VkImageSubresourceRange()
            {
                aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                baseArrayLayer = 0,
                layerCount = 1,
                levelCount = 1
            }
        };
        
        uint mipWidth = width;
        uint mipHeight = height;

        // For each mip level resize the image
        for (uint i = 1; i < mipMapLevels; i++) {
            memoryBarrier.subresourceRange.baseMipLevel = i - 1;
            memoryBarrier.oldLayout = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
            memoryBarrier.newLayout = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL;
            memoryBarrier.srcAccessMask = VkAccessFlags.VK_ACCESS_TRANSFER_WRITE_BIT;
            memoryBarrier.dstAccessMask = VkAccessFlags.VK_ACCESS_TRANSFER_READ_BIT;

            VulkanNative.vkCmdPipelineBarrier(commandBuffer,
                VkPipelineStageFlags.VK_PIPELINE_STAGE_TRANSFER_BIT, VkPipelineStageFlags.VK_PIPELINE_STAGE_TRANSFER_BIT, 0,
                0, null,
                0, null,
                1, &memoryBarrier);

            VkImageBlit blit = new VkImageBlit()
            {
                srcOffsets_0 = new VkOffset3D()
                {
                    x = 0,
                    y = 0,
                    z = 0
                },
                srcOffsets_1 = new VkOffset3D()
                {
                    x = (int)mipWidth,
                    y = (int)mipHeight,
                    z = 1
                },
                srcSubresource = new VkImageSubresourceLayers()
                {
                    aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                    mipLevel = i - 1,
                    baseArrayLayer = 0,
                    layerCount = 1,
                },
                dstOffsets_0 = new VkOffset3D()
                {
                    x = 0,
                    y = 0,
                    z = 0
                },
                dstOffsets_1 = new VkOffset3D()
                {
                    x = (int)(mipWidth > 1 ? mipWidth / 2 : 1),
                    y = (int)(mipHeight > 1 ? mipHeight / 2 : 1),
                    z = 1
                },
                dstSubresource = new VkImageSubresourceLayers()
                {
                    aspectMask = VkImageAspectFlags.VK_IMAGE_ASPECT_COLOR_BIT,
                    mipLevel = i,
                    baseArrayLayer = 0,
                    layerCount = 1
                }
            };

            VulkanNative.vkCmdBlitImage(commandBuffer,
                image, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                image, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                1, &blit,
                VkFilter.VK_FILTER_LINEAR);

            memoryBarrier.oldLayout = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL;
            memoryBarrier.newLayout = VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
            memoryBarrier.srcAccessMask = VkAccessFlags.VK_ACCESS_TRANSFER_READ_BIT;
            memoryBarrier.dstAccessMask = VkAccessFlags.VK_ACCESS_SHADER_READ_BIT;
            
            VulkanNative.vkCmdPipelineBarrier(commandBuffer,
                VkPipelineStageFlags.VK_PIPELINE_STAGE_TRANSFER_BIT, VkPipelineStageFlags.VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0,
                0, null,
                0, null,
                1, &memoryBarrier);

            if (mipWidth > 1) mipWidth /= 2;
            if (mipHeight > 1) mipHeight /= 2;
        }

        // Set base mip level and transition the layout of the texture
        memoryBarrier.subresourceRange.baseMipLevel = mipMapLevels - 1;
        memoryBarrier.oldLayout = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
        memoryBarrier.newLayout = VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
        memoryBarrier.srcAccessMask = VkAccessFlags.VK_ACCESS_TRANSFER_WRITE_BIT;
        memoryBarrier.dstAccessMask = VkAccessFlags.VK_ACCESS_SHADER_READ_BIT;

        // Bind the image barrier and apply the changes
        VulkanNative.vkCmdPipelineBarrier(commandBuffer,
            VkPipelineStageFlags.VK_PIPELINE_STAGE_TRANSFER_BIT, VkPipelineStageFlags.VK_PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0,
            0, null,
            0, null,
            1, &memoryBarrier);
        
        // End the current command buffer
        VulkanUtilities.EndSingleTimeCommands(commandBuffer);
    }

    public void CleanUp()
    {
        image.CleanUp();
    }
    
    private uint TextureTypeToBinding(in TextureType givenTextureType)
    {
        return (uint) givenTextureType;
    }
}