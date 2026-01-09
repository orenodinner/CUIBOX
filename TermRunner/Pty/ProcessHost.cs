using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TermRunner.Pty;

public sealed class ProcessHost : IDisposable
{
    private IntPtr _processHandle;
    private IntPtr _threadHandle;
    private bool _disposed;

    private ProcessHost(IntPtr processHandle, IntPtr threadHandle, uint processId)
    {
        _processHandle = processHandle;
        _threadHandle = threadHandle;
        ProcessId = processId;
    }

    public uint ProcessId { get; }

    public static ProcessHost StartAttachedToPty(IntPtr pseudoConsole, string commandLine, string workingDirectory)
    {
        var startupInfo = new NativeMethods.STARTUPINFOEX
        {
            StartupInfo = new NativeMethods.STARTUPINFO
            {
                cb = Marshal.SizeOf<NativeMethods.STARTUPINFOEX>()
            }
        };

        IntPtr attributeSize = IntPtr.Zero;
        NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeSize);
        var attributeList = Marshal.AllocHGlobal(attributeSize);
        IntPtr ptyHandle = IntPtr.Zero;

        try
        {
            if (!NativeMethods.InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeSize))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            ptyHandle = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(ptyHandle, pseudoConsole);

            if (!NativeMethods.UpdateProcThreadAttribute(
                attributeList,
                0,
                (IntPtr)NativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                ptyHandle,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            startupInfo.lpAttributeList = attributeList;

            if (!NativeMethods.CreateProcessW(
                null,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                true,
                NativeMethods.EXTENDED_STARTUPINFO_PRESENT | NativeMethods.CREATE_UNICODE_ENVIRONMENT,
                IntPtr.Zero,
                workingDirectory,
                ref startupInfo,
                out var processInformation))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return new ProcessHost(processInformation.hProcess, processInformation.hThread, processInformation.dwProcessId);
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                NativeMethods.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (ptyHandle != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(ptyHandle);
            }
        }
    }

    public void Terminate()
    {
        if (_processHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.TerminateProcess(_processHandle, 0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_threadHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_threadHandle);
            _threadHandle = IntPtr.Zero;
        }

        if (_processHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }
    }
}
