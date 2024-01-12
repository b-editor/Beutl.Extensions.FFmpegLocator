﻿using Avalonia.Controls;
using Avalonia.Media;

using Beutl.Extensibility;

using FluentAvalonia.UI.Controls;

namespace Beutl.Extensions.FFmpegLocator;

[Export]
public class LocateFFmpegPageExtension : PageExtension
{
    public override string Name => "FFmpeg Locator";

    public override string DisplayName => "FFmpegを配置";

    public override IPageContext CreateContext()
    {
        return new LocateFFmpegPageContext(this);
    }

    public override Control CreateControl()
    {
        return new LocateFFmpegPage();
    }

    public override IconSource GetFilledIcon()
    {
        return new PathIconSource()
        {
            Data = Geometry.Parse("M0.567749 0L12.349 0L6.36437 5.8873L4.26775 7.94983L4.26775 10.8521L8.07207 7.25346L15.74 0L25.6355 0L13.7998 11.8357L6.03551 19.6L8.77532 19.6L15.2942 13.0312L23.8677 4.39191L23.8677 14.2346L20.9099 17.5237L19.0427 19.6L23.5053 19.6L25.0677 19.6L25.0678 22.1L13.4323 22.1L18.9545 15.9594L21.3677 13.2758L21.3677 10.4602L17.2576 14.6019L9.81645 22.1L-1.4782e-05 22.1L11.8357 10.2643L19.6 2.5L16.7351 2.5L10.0431 8.83028L1.76775 16.6583L1.76775 6.90227L4.39833 4.31446L6.24278 2.5L0.567749 2.5L0.567749 0L0.567749 0Z")
        };
    }

    public override IconSource GetRegularIcon()
    {
        return new PathIconSource()
        {
            Data = Geometry.Parse("M-687.914 3.07483L-685.818 1.0123L-680.596 -4.125L-679.833 -4.875L-691.615 -4.875L-691.615 -2.375L-685.939 -2.375L-686.702 -1.625L-687.784 -0.560536L-690.414 2.02727L-690.414 11.7833L-689.664 11.0739L-682.139 3.95528L-675.447 -2.375L-672.582 -2.375L-673.332 -1.625L-692.182 17.225L-682.366 17.225L-674.925 9.72686L-671.565 6.34093L-670.815 5.58518L-670.815 8.40084L-673.228 11.0844L-678.075 16.475L-678.75 17.225L-677.741 17.225L-667.865 17.225L-667.115 17.225L-667.115 16.475L-667.115 15.475L-667.115 14.725L-667.865 14.725L-668.677 14.725L-672.131 14.725L-673.139 14.725L-672.465 13.975L-671.272 12.6487L-668.315 9.35961L-668.315 -0.483086L-669.065 0.272663L-676.888 8.15617L-683.407 14.725L-686.147 14.725L-685.397 13.975L-667.297 -4.125L-666.547 -4.875L-676.442 -4.875L-684.11 2.37846L-687.164 5.26768L-687.914 5.97714L-687.914 3.07483L-687.914 3.07483Z")
        };
    }
}