using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace NetTimeService;

public static class SystemClock
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;

        public SYSTEMTIME(DateTime dt)
        {
            dt = dt.ToUniversalTime();
            wYear = (ushort)dt.Year;
            wMonth = (ushort)dt.Month;
            wDayOfWeek = (ushort)dt.DayOfWeek;
            wDay = (ushort)dt.Day;
            wHour = (ushort)dt.Hour;
            wMinute = (ushort)dt.Minute;
            wSecond = (ushort)dt.Second;
            wMilliseconds = (ushort)dt.Millisecond;
        }

        public DateTime ToDateTime()
        {
            return new DateTime(wYear, wMonth, wDay, wHour, wMinute, wSecond, wMilliseconds, DateTimeKind.Utc);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetSystemTime(ref SYSTEMTIME lpSystemTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern void GetSystemTime(out SYSTEMTIME lpSystemTime);

    public static bool SetSystemTime(DateTime utcTime, ILogger? logger = null)
    {
        return SetTime(utcTime, logger);
    }

    public static bool UpdateUtcTime(DateTime newUtcTime, ILogger? logger = null)
    {
        return SetTime(newUtcTime, logger);
    }

    public static bool SetTime(DateTime utcTime, ILogger? logger = null)
    {
        SYSTEMTIME st = new SYSTEMTIME(utcTime);
        bool result = SetSystemTime(ref st);
        if (!result)
        {
            int lastError = Marshal.GetLastWin32Error();
            string errorMessage = $"SetSystemTime failed with Win32 error code: {lastError} (0x{lastError:X8}).";
            if (logger != null)
            {
                logger.LogError(errorMessage);
            }
            else
            {
                Console.Error.WriteLine(errorMessage);
            }
        }
        return result;
    }
}