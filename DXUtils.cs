using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using System.IO;
using System;
using Direct3D11 = SharpDX.Direct3D11;
using Direct3D = SharpDX.Direct3D;

namespace DX.WPF
{
	public static class DXUtils
	{
		#region GetOrThrow<T>()

		public static T GetOrThrow<T>(this T obj)
			where T : class, IDisposable
		{
			if (obj == null)
				throw new ObjectDisposedException(typeof(T).Name);
			return obj;
		} 

		#endregion

		#region D3D10, D3D11: CreateBuffer<T>()

		public static Direct3D11.Buffer CreateBuffer<T>(this Direct3D11.Device device, T[] range)
			where T : struct
		{
			int sizeInBytes = Marshal.SizeOf(typeof(T));
			using (var stream = new DataStream(range.Length * sizeInBytes, true, true))
			{
				stream.WriteRange(range);
				return new Direct3D11.Buffer(device, stream, new Direct3D11.BufferDescription
				{
					BindFlags = Direct3D11.BindFlags.VertexBuffer,
					SizeInBytes = (int)stream.Length,
					CpuAccessFlags = Direct3D11.CpuAccessFlags.None,
					OptionFlags = Direct3D11.ResourceOptionFlags.None,
					StructureByteStride = 0,
					Usage = Direct3D11.ResourceUsage.Default,
				});
			}
		}

        #endregion
    }
}
