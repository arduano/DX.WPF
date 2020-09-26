using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DX.WPF
{
    public abstract partial class D3D : IDirect3D, IDisposable
    {
        class ArgPointer { public DrawEventArgs args = null; }

        [DllImport("ntdll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int NtDelayExecution([MarshalAs(UnmanagedType.I1)] bool alertable, ref Int64 DelayInterval);

        readonly ArgPointer argsPointer = new ArgPointer();

        Task renderThread = null;

        List<DateTime> frameTimes = new List<DateTime>();

        public int FPSLock { get; set; } = 60;
        public bool SingleThreadedRender { 
            get => runner == null;
            set
            {
                var current = SingleThreadedRender;
                if (disposed) current = false;
                if (current == value) return;
                if (!value)
                {
                    var t = new Thread(() =>
                    {
                        runner = Dispatcher.CurrentDispatcher;
                        Dispatcher.Run();

                        Console.WriteLine("Dispatcher closed");
                    });

                    t.Start();

                    SpinWait.SpinUntil(() => runner != null);
                }
                else
                {
                    runner?.BeginInvokeShutdown(DispatcherPriority.Send);
                    runner = null;
                    renderThread?.Wait();
                    renderThread = null;
                }
            } 
        }
        Stopwatch frameTimer = new Stopwatch();
        double delayExtraDelay = 0;

        Stopwatch renderTimer = new Stopwatch();

        public D3D()
        {
            OnInteractiveInit();
        }

        partial void OnInteractiveInit();

        ~D3D() { Dispose(); }

        bool disposed = false;

        public virtual void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SingleThreadedRender = true;
        }

        public Vector2 RenderSize { get; protected set; }

        Dispatcher runner = null;

        public virtual void Reset(DrawEventArgs args)
        {
            Action run = () =>
            {
                lock (argsPointer)
                {
                    int w = (int)Math.Ceiling(args.RenderSize.Width);
                    int h = (int)Math.Ceiling(args.RenderSize.Height);
                    if (w < 1 || h < 1)
                        return;

                    RenderSize = new Vector2(w, h);

                    Reset(w, h);
                    if (Resetted != null)
                        Resetted(this, args);

                    argsPointer.args = args;
                    Render(args);

                    if (args.Target != null)
                        Application.Current.Dispatcher.Invoke(() => SetBackBuffer(args.Target));
                }
            };

            if (runner != null)
            {
                runner.InvokeAsync(run, DispatcherPriority.Send);
            }
            else
            {
                run();
            }
        }

        public virtual void Reset(int w, int h)
        {
        }

        public event EventHandler<DrawEventArgs> Resetted;

        public static void Set<T>(ref T field, T newValue)
            where T : IDisposable
        {
            if (field != null)
                field.Dispose();
            field = newValue;
        }

        public abstract void SetBackBuffer(DXImageSource dximage);

        public TimeSpan RenderTime { get; protected set; }

        public void Render(DrawEventArgs args)
        {
            RenderTime = args.TotalTime;

            if (SingleThreadedRender)
            {
                lock (argsPointer)
                {
                    TrueRender();
                }
            }
            else
            {
                if (renderThread == null || renderThread.IsCompleted)
                {
                    renderThread = StartRenderThread();
                }
            }
            argsPointer.args = args;
        }

        Task StartRenderThread()
        {
            return
            Task.Run(() =>
            {
                try
                {

                    renderTimer.Start();
                    TimeSpan last = renderTimer.Elapsed;
                    frameTimes.Add(DateTime.UtcNow);
                    while (!SingleThreadedRender)
                    {
                        frameTimer.Start();
                        while (SingleThreadedRender)
                        {
                            Thread.Sleep(100);
                            if (SingleThreadedRender) return;
                        }

                        try
                        {
                            if (runner.HasShutdownStarted) return;
                            runner.Invoke(() =>
                            {
                                lock (argsPointer)
                                {
                                    if (argsPointer.args == null || SingleThreadedRender) Thread.Sleep(100);
                                    else
                                    {
                                        argsPointer.args.TotalTime = renderTimer.Elapsed;
                                        argsPointer.args.DeltaTime = renderTimer.Elapsed - last;
                                        last = renderTimer.Elapsed;
                                        TrueRender();
                                    }
                                }
                            });
                        }
                        catch (OperationCanceledException e)
                        { }

                        if (FPSLock != 0)
                        {
                            var desired = 10000000 / FPSLock;
                            var elapsed = frameTimer.ElapsedTicks;
                            long remaining = -(desired + (long)delayExtraDelay - elapsed);
                            Stopwatch s = new Stopwatch();
                            s.Start();
                            if (remaining < 0)
                            {
                                Thread.Sleep((int)(remaining / -10000));
                                //NtDelayExecution(false, ref remaining);
                            }
                            var excess = desired - frameTimer.ElapsedTicks;
                            delayExtraDelay = (delayExtraDelay * 60 + excess) / 61;
                        }
                        frameTimer.Reset();
                    }
                }
                catch (Exception e)
                { }
            });
        }

        void TrueRender()
        {
            BeginRender(argsPointer.args);
            RenderScene(argsPointer.args);
            EndRender(argsPointer.args);
        }

        public virtual void BeginRender(DrawEventArgs args) { }
        public virtual void RenderScene(DrawEventArgs args)
        {
            Rendering?.Invoke(this, args);
        }
        public virtual void EndRender(DrawEventArgs args) { }

        public event EventHandler<DrawEventArgs> Rendering;
    }
}
