// Stand-in for SlimDX (a Windows-only C++/CLI DirectInput wrapper).
// Exposes only the members JumpKing.exe references and reports no DirectInput
// devices, so the game falls back to MonoGame's SDL GamePad support on macOS.
using System;
using System.Collections.Generic;
using System.Reflection;

[assembly: AssemblyVersion("4.0.13.43")]

namespace SlimDX
{
    public struct Result
    {
        public int Code { get { return 0; } }
        public bool IsSuccess { get { return true; } }
        public bool IsFailure { get { return false; } }
    }

    public class SlimDXException : Exception
    {
        public SlimDXException() { }
        public SlimDXException(string message) : base(message) { }
    }
}

namespace SlimDX.DirectInput
{
    public enum DeviceClass { All = 0, Device = 1, Pointer = 2, Keyboard = 3, GameController = 4 }

    [Flags]
    public enum DeviceEnumerationFlags { AllDevices = 0, AttachedOnly = 1, ForceFeedback = 256, IncludeAliases = 65536, IncludePhantoms = 131072, IncludeHidden = 262144 }

    [Flags]
    public enum ObjectDeviceType { All = 0, RelativeAxis = 1, AbsoluteAxis = 2, Axis = 3, PushButton = 4, ToggleButton = 8, Button = 12, PointOfViewController = 16 }

    public class DirectInputException : SlimDXException
    {
        public DirectInputException() { }
        public DirectInputException(string message) : base(message) { }
    }

    public class DirectInput : IDisposable
    {
        public DirectInput() { }

        public IList<DeviceInstance> GetDevices(DeviceClass deviceClass, DeviceEnumerationFlags enumerationFlags)
        {
            return new List<DeviceInstance>();
        }

        public void Dispose() { }
    }

    public class DeviceInstance
    {
        public Guid InstanceGuid { get { return Guid.Empty; } }
        public Guid ProductGuid { get { return Guid.Empty; } }
        public string ProductName { get { return string.Empty; } }
        public string InstanceName { get { return string.Empty; } }
        public bool HumanInterfaceDevice { get { return false; } }
    }

    public struct DeviceObjectInstance
    {
        public ObjectDeviceType ObjectType { get { return ObjectDeviceType.All; } }
        public int ObjectTypeId { get { return 0; } }
        public string Name { get { return string.Empty; } }
    }

    public class Capabilities
    {
        public int ButtonCount { get { return 0; } }
        public int AxesCount { get { return 0; } }
        public int PovCount { get { return 0; } }
    }

    public class DeviceProperties
    {
        public string InstanceName { get { return string.Empty; } }
        public string ProductName { get { return string.Empty; } }
    }

    public class ObjectProperties
    {
        public void SetRange(int lowerBound, int upperBound) { }
    }

    public class Device : IDisposable
    {
        public Capabilities Capabilities { get { return new Capabilities(); } }
        public DeviceInstance Information { get { return new DeviceInstance(); } }
        public DeviceProperties Properties { get { return new DeviceProperties(); } }

        public Result Acquire() { throw new DirectInputException("DirectInput is not available on this platform."); }
        public Result Unacquire() { return new Result(); }
        public IList<DeviceObjectInstance> GetObjects() { return new List<DeviceObjectInstance>(); }
        public ObjectProperties GetObjectPropertiesById(int objectId) { return new ObjectProperties(); }
        public void Dispose() { }
    }

    public class Joystick : Device
    {
        public Joystick(DirectInput directInput, Guid subsystem)
        {
            throw new DirectInputException("DirectInput is not available on this platform.");
        }

        public JoystickState GetCurrentState() { return new JoystickState(); }
    }

    public class JoystickState
    {
        public int X { get { return 0; } }
        public int Y { get { return 0; } }
        public int Z { get { return 0; } }
        public int RotationX { get { return 0; } }
        public int RotationY { get { return 0; } }
        public int RotationZ { get { return 0; } }
        public bool[] GetButtons() { return new bool[128]; }
        public int[] GetPointOfViewControllers() { return new int[] { -1, -1, -1, -1 }; }
    }
}
