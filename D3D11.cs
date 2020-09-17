using System;
using System.Collections.Generic;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using SharpDX.Mathematics.Interop;

namespace DX.WPF
{
    public class D3D11 : D3D
    {
        protected Device device;

        public D3D11(FeatureLevel minLevel)
        {
            device = DeviceUtil.Create11(DeviceCreationFlags.BgraSupport, minLevel);
            if (device == null)
                throw new NotSupportedException();
        }

        public D3D11()
        {
            device = DeviceUtil.Create11(DeviceCreationFlags.BgraSupport);
            if (device == null)
                throw new NotSupportedException();
        }

        public D3D11(Device device)
        {
            this.device = device;
            if (device == null)
                throw new NotSupportedException();
        }

        public D3D11(Adapter a)
        {
            if (a == null)
            {
                device = DeviceUtil.Create11(DeviceCreationFlags.BgraSupport, FeatureLevel.Level_11_0);
                if (device == null)
                    throw new NotSupportedException();
            }
            device = new Device(a);
        }

        public override void Dispose()
        {
            base.Dispose();
            Set(ref device, null);
            Set(ref renderTarget, null);
            Set(ref renderTargetView, null);
        }

        public Device Device => device.GetOrThrow();

        public bool IsDisposed => device == null;

        public override void SetBackBuffer(DXImageSource dximage) => dximage.SetBackBuffer(RenderTarget);

        protected Texture2D renderTarget;
        protected RenderTargetView renderTargetView;
        protected Texture2D renderTargetCopy;

        #region RenderTargetOptionFlags

        public ResourceOptionFlags RenderTargetOptionFlags { get; set; } = ResourceOptionFlags.Shared;

        #endregion

        public override void Reset(int w, int h)
        {
            device.GetOrThrow();

            if (w < 1)
                throw new ArgumentOutOfRangeException("w");
            if (h < 1)
                throw new ArgumentOutOfRangeException("h");

            var desc = new Texture2DDescription
            {
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Format = Format.B8G8R8A8_UNorm,
                Width = w,
                Height = h,
                MipLevels = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                OptionFlags = RenderTargetOptionFlags,
                CpuAccessFlags = CpuAccessFlags.None,
                ArraySize = 1
            };
            Set(ref renderTarget, new Texture2D(this.device, desc));
            Set(ref renderTargetView, new RenderTargetView(this.device, this.renderTarget));
            Set(ref renderTargetCopy, new Texture2D(this.device, desc));
        }

        public override void BeginRender(DrawEventArgs args)
        {
            device.ImmediateContext.Rasterizer.SetViewports(new RawViewportF[] { new RawViewportF() {
                X = 0,
                Y = 0,
                Width = (int)args.RenderSize.Width,
                Height = (int)args.RenderSize.Height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            } });

            device.ImmediateContext.OutputMerger.SetRenderTargets(renderTargetView);

            device.GetOrThrow();
        }

        public override void EndRender(DrawEventArgs args)
        {
            Device.ImmediateContext.Flush();
            Device.ImmediateContext.CopyResource(renderTarget, renderTargetCopy);
        }

        protected T Prepared<T>(ref T property)
        {
            device.GetOrThrow();
            if (property == null)
                Reset(1, 1);
            return property;
        }

        public Texture2D RenderTarget => Prepared(ref renderTargetCopy);
        public RenderTargetView RenderTargetView => Prepared(ref renderTargetView);
    }
}
