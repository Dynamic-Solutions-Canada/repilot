using System.Runtime.InteropServices;

namespace Repilot.Classes;

/// <summary>
/// Extracts a shell item's icon (e.g. a <c>shell:AppsFolder\{AUMID}</c> app) as
/// top-down 32bpp BGRA pixels, via IShellItemImageFactory. Self-contained interop.
/// </summary>
internal static class ShellIcons
{
    public sealed record IconPixels(byte[] Bgra, int Width, int Height);

    public static IconPixels? Extract(string parsingName, int size)
    {
        var iid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"); // IID_IShellItemImageFactory
        if (SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out var factory) != 0 || factory == null)
        {
            return null;
        }

        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            if (factory.GetImage(new SIZE { cx = size, cy = size }, 0, out hBitmap) != 0 || hBitmap == IntPtr.Zero)
            {
                return null;
            }

            GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), out var bm);
            int w = bm.bmWidth, h = bm.bmHeight;
            if (w <= 0 || h <= 0)
            {
                return null;
            }

            // BitBlt the source HBITMAP into a top-down 32bpp DIB we control, to
            // normalize orientation/format, then copy out the BGRA bytes.
            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = w,
                    biHeight = -h, // negative = top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0,
                }
            };

            IntPtr hdcSrc = CreateCompatibleDC(IntPtr.Zero);
            IntPtr hdcDst = CreateCompatibleDC(IntPtr.Zero);
            IntPtr hDib = CreateDIBSection(hdcDst, ref bmi, 0, out IntPtr dibBits, IntPtr.Zero, 0);
            try
            {
                if (hDib == IntPtr.Zero || dibBits == IntPtr.Zero)
                {
                    return null;
                }

                IntPtr oldSrc = SelectObject(hdcSrc, hBitmap);
                IntPtr oldDst = SelectObject(hdcDst, hDib);
                BitBlt(hdcDst, 0, 0, w, h, hdcSrc, 0, 0, 0x00CC0020 /* SRCCOPY */);
                SelectObject(hdcSrc, oldSrc);
                SelectObject(hdcDst, oldDst);

                var bytes = new byte[w * h * 4];
                Marshal.Copy(dibBits, bytes, 0, bytes.Length);
                return new IconPixels(bytes, w, h);
            }
            finally
            {
                if (hDib != IntPtr.Zero)
                {
                    DeleteObject(hDib);
                }

                DeleteDC(hdcSrc);
                DeleteDC(hdcDst);
            }
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }

            Marshal.ReleaseComObject(factory);
        }
    }

    // ── interop ───────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx; public int cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP { public int bmType; public int bmWidth; public int bmHeight; public int bmWidthBytes; public ushort bmPlanes; public ushort bmBitsPixel; public IntPtr bmBits; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth; public int biHeight; public ushort biPlanes; public ushort biBitCount;
        public uint biCompression; public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter; public uint biClrUsed; public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll")] private static extern int GetObject(IntPtr h, int c, out BITMAP pv);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr ho);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
}
