using System;
using System.Runtime.InteropServices;

namespace CodeViewer.ShellExtension;

public enum SIGDN : uint
{
    SIGDN_NORMALDISPLAY = 0x00000000,
    SIGDN_PARENTRELATIVEPARSING = 0x80018001,
    SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
    SIGDN_PARENTRELATIVEEDITING = 0x80031001,
    SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
    SIGDN_FILESYSPATH = 0x80058000,
    SIGDN_URL = 0x80068000,
    SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
    SIGDN_PARENTRELATIVE = 0x80080001
}

public enum EXPCMDSTATE : uint
{
    ECS_ENABLED = 0x0,
    ECS_DISABLED = 0x1,
    ECS_HIDDEN = 0x2,
    ECS_CHECKBOX = 0x4,
    ECS_CHECKED = 0x8,
    ECS_RADIOCHECK = 0x10
}

public enum EXPCMDFLAGS : uint
{
    ECF_DEFAULT = 0x0,
    ECF_HASSUBCOMMANDS = 0x1,
    ECF_HASSPLITBUTTON = 0x2,
    ECF_HIDELABEL = 0x4,
    ECF_ISSEPARATOR = 0x8,
    ECF_HASCHECKBOX = 0x10,
    ECF_SEPARATORBEFORE = 0x20,
    ECF_SEPARATORAFTER = 0x40,
    ECF_ISNOTUSERY = 0x80
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
public interface IShellItem
{
    [PreserveSig]
    int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetParent(out IShellItem ppsi);

    [PreserveSig]
    int GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);

    [PreserveSig]
    int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

    [PreserveSig]
    int Compare(IShellItem psi, uint hint, out int piOrder);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
public interface IShellItemArray
{
    [PreserveSig]
    int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);

    [PreserveSig]
    int GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetPropertyDescriptionList(IntPtr keyType, ref Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetAttributes(int AttribFlags, uint sfgaoMask, out uint psfgaoAttribs);

    [PreserveSig]
    int GetCount(out uint pdwNumItems);

    [PreserveSig]
    int GetItemAt(uint dwIndex, out IShellItem ppsi);

    [PreserveSig]
    int EnumItems(out IntPtr ppenumShellItems);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0db")]
public interface IExplorerCommand
{
    [PreserveSig]
    int GetTitle(IShellItemArray? psiItemArray, out IntPtr ppszName);

    [PreserveSig]
    int GetIcon(IShellItemArray? psiItemArray, out IntPtr ppszIcon);

    [PreserveSig]
    int GetToolTip(IShellItemArray? psiItemArray, out IntPtr ppszInfo);

    [PreserveSig]
    int GetCanonicalName(out Guid pguidCommandName);

    [PreserveSig]
    int GetState(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow, out EXPCMDSTATE pdwCmdState);

    [PreserveSig]
    int Invoke(IShellItemArray? psiItemArray, [MarshalAs(UnmanagedType.IUnknown)] object? pbc);

    [PreserveSig]
    int GetFlags(out EXPCMDFLAGS pdwFlags);

    [PreserveSig]
    int EnumSubCommands(out IntPtr ppEnum);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("924502db-da24-44ac-8625-78322c366ffc")]
public interface IObjectWithSelection
{
    [PreserveSig]
    int SetSelection([MarshalAs(UnmanagedType.IUnknown)] object? punk);

    [PreserveSig]
    int GetSelection(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object? ppv);
}
