using System.ComponentModel;
using System.Runtime.InteropServices;
namespace MistikLauncher;
public static class WindowsSecret
{
    [StructLayout(LayoutKind.Sequential)] struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll",SetLastError=true)] static extern bool CryptProtectData(ref Blob data,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob result);
    [DllImport("crypt32.dll",SetLastError=true)] static extern bool CryptUnprotectData(ref Blob data,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob result);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
    public static byte[] Transform(byte[] bytes,bool protect)
    {
        var pin=GCHandle.Alloc(bytes,GCHandleType.Pinned); var input=new Blob { Length=bytes.Length,Data=pin.AddrOfPinnedObject() }; Blob output=default;
        try {
            bool ok=protect?CryptProtectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
            var result=new byte[output.Length]; Marshal.Copy(output.Data,result,0,result.Length); return result;
        }
        finally { if(output.Data!=IntPtr.Zero) LocalFree(output.Data); pin.Free(); }
    }
}
