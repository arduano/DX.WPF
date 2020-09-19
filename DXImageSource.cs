using System;
using System.Windows;
using System.Windows.Interop;
using SharpDX.Direct3D9;

namespace DX.WPF
{
    public class DXImageSource : D3DImage, IDisposable
    {
        ~DXImageSource() { Dispose(); }
        public DXImageSource()
        {
            StartD3D9();
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            Dispatcher.Invoke(() =>
            {
                SetBackBuffer((Texture)null);
            });
            EndD3D9();
            isDisposed = true;
        }
        bool isDisposed;

        public bool IsDisposed { get { return isDisposed; } }

        public void Invalidate()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);

            if (backBuffer != null)
            {
                Lock();
                AddDirtyRect(new Int32Rect(0, 0, base.PixelWidth, base.PixelHeight));
                Unlock();
            }
        }

        SharpDX.Direct3D11.Texture2D lastTexture = null;
        public void SetBackBuffer(SharpDX.Direct3D11.Texture2D texture)
        {
            lastTexture = texture;
            SetBackBuffer(DXSharing.GetSharedD3D9(d3d9.Device, texture));
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (lastTexture != null)
            {
                SetBackBuffer(lastTexture);
            }
        }

        Texture backBuffer;

        public void SetBackBuffer(Texture texture)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);

            Texture toDelete = null;
            try
            {
                if (texture != backBuffer)
                {
                    // if it's from the private (SDX9ImageSource) D3D9 device, dispose of it
                    if (backBuffer != null)
                    {
                        if (backBuffer.IsDisposed)
                            backBuffer = null;
                        else if (backBuffer.Device.NativePointer == d3d9.Device.NativePointer)
                            toDelete = backBuffer;
                    }
                    backBuffer = texture;
                }

                if (texture != null)
                {
                    using (Surface surface = texture.GetSurfaceLevel(0))
                    {
                        Lock();
                        SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer);
                        AddDirtyRect(new Int32Rect(0, 0, base.PixelWidth, base.PixelHeight));
                        Unlock();
                    }
                }
                else
                {
                    Lock();
                    SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                    Unlock();
                }
            }
            finally
            {
                if (toDelete != null)
                {
                    toDelete.Dispose();
                }
            }
        }

        #region (private, static / shared) D3D9: d3d9

        static int activeClients;
        static D3D9 d3d9;

        private static void StartD3D9()
        {
            if (activeClients == 0)
                d3d9 = new D3D9();
            activeClients++;
        }

        private static void EndD3D9()
        {
            activeClients--;
            if (activeClients == 0)
                d3d9.Dispose();
        }

        #endregion
    }
}
