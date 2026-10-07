using Microsoft.Xna.Framework.Graphics;
using SuperCricket.Content;
using SuperCricket.Game.Rendering;

namespace SuperCricket.Game.Tests;

public sealed class PlayerTextureSamplerTests
{
    [Fact]
    public void CreateSamplerState_MapsGltfWrapAndFilterSettings()
    {
        using var sampler = SkinnedPlayerRenderer.CreateSamplerState(new PlayerTextureData
        {
            MimeType = "image/png",
            Content = [1],
            MinFilter = 9985,
            MagFilter = 9728,
            WrapU = 33648,
            WrapV = 33071
        });

        Assert.Equal(TextureFilter.MinLinearMagPointMipPoint, sampler.Filter);
        Assert.Equal(TextureAddressMode.Mirror, sampler.AddressU);
        Assert.Equal(TextureAddressMode.Clamp, sampler.AddressV);
        Assert.Equal(TextureAddressMode.Clamp, sampler.AddressW);
    }

    [Fact]
    public void CreateSamplerState_MapsLinearRepeatDefaults()
    {
        using var sampler = SkinnedPlayerRenderer.CreateSamplerState(new PlayerTextureData
        {
            MimeType = "image/png",
            Content = [1]
        });

        Assert.Equal(TextureFilter.Linear, sampler.Filter);
        Assert.Equal(TextureAddressMode.Wrap, sampler.AddressU);
        Assert.Equal(TextureAddressMode.Wrap, sampler.AddressV);
    }
}
