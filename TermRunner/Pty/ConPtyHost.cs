using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TermRunner.Pty;

public sealed class ConPtyHost : IDisposable
{
    private bool _disposed;

    private ConPtyHost(IntPtr pseudoConsole, SafeFileHandle inputWriter, SafeFileHandle outputReader)
    {
        PseudoConsole = pseudoConsole;
        InputWriter = inputWriter;
        OutputReader = outputReader;
    }

    public IntPtr PseudoConsole { get; private set; }

    public SafeFileHandle InputWriter { get; }

    public SafeFileHandle OutputReader { get; }

    public static ConPtyHost Create(int cols, int rows)
    {
        cols = Math.Max(cols, 1);
        rows = Math.Max(rows, 1);

        var sa = new NativeMethods.SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf<NativeMethods.SECURITY_ATTRIBUTES>(),
            bInheritHandle = false,
            lpSecurityDescriptor = IntPtr.Zero
        };

        if (!NativeMethods.CreatePipe(out var inputRead, out var inputWrite, ref sa, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (!NativeMethods.CreatePipe(out var outputRead, out var outputWrite, ref sa, 0))
        {
            NativeMethods.CloseHandle(inputRead);
            NativeMethods.CloseHandle(inputWrite);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var size = new NativeMethods.COORD((short)cols, (short)rows);
        var hr = NativeMethods.CreatePseudoConsole(size, inputRead, outputWrite, 0, out var hPc);
        if (hr != 0)
        {
            NativeMethods.CloseHandle(inputRead);
            NativeMethods.CloseHandle(inputWrite);
            NativeMethods.CloseHandle(outputRead);
            NativeMethods.CloseHandle(outputWrite);
            Marshal.ThrowExceptionForHR(hr);
        }

        NativeMethods.CloseHandle(inputRead);
        NativeMethods.CloseHandle(outputWrite);

        var inputWriter = new SafeFileHandle(inputWrite, ownsHandle: true);
        var outputReader = new SafeFileHandle(outputRead, ownsHandle: true);
        return new ConPtyHost(hPc, inputWriter, outputReader);
    }

    public void Resize(int cols, int rows)
    {
        if (PseudoConsole == IntPtr.Zero)
        {
            return;
        }

        cols = Math.Max(cols, 1);
        rows = Math.Max(rows, 1);
        var size = new NativeMethods.COORD((short)cols, (short)rows);
        var hr = NativeMethods.ResizePseudoConsole(PseudoConsole, size);
        if (hr != 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        InputWriter.Dispose();
        OutputReader.Dispose();

        if (PseudoConsole != IntPtr.Zero)
        {
            NativeMethods.ClosePseudoConsole(PseudoConsole);
            PseudoConsole = IntPtr.Zero;
        }
    }
}

internal static class NativeMethods
{
    internal const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    internal const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    internal const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

    [StructLayout(LayoutKind.Sequential)]
    internal struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct COORD
    {
        public readonly short X;
        public readonly short Y;

        public COORD(short x, short y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ReadFile(SafeFileHandle hFile, byte[] lpBuffer, int nNumberOfBytesToRead, out int lpNumberOfBytesRead, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer, int nNumberOfBytesToWrite, out int lpNumberOfBytesWritten, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CancelIoEx(SafeFileHandle hFile, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool CreateProcessW(
        string? lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList,
        uint dwFlags,
        IntPtr attribute,
        IntPtr lpValue,
        IntPtr cbSize,
        IntPtr lpPreviousValue,
        IntPtr lpReturnSize);

    [DllImport("kernel32.dll")]
    internal static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);
}
