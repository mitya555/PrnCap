using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PrnCap
{
    using System.Management;
    class WmiWrapper
    {
        static IEnumerable<ManagementObject> Query(string className)
        {
            return new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM " + className)
                .Get().Cast<ManagementObject>();
        }
        static ManagementPath Create(string className, params object[] property)
        {
            var _class = new ManagementClass("root\\cimv2", className, new ObjectGetOptions());
            _class.Get();
            var _obj = _class.CreateInstance();
            for (int i = 0; i < property.Length - 1; i += 2)
                _obj.SetPropertyValue("" + property[i], property[i + 1]);
            return _obj.Put();
        }
        const string PRINTER_NAME = "prncap localhost printer";
        const string PORT_NAME = "prncap localhost printer port";
        public static void InstallPrinter()
        {
            var _printer = Query("Win32_Printer").FirstOrDefault(o => PRINTER_NAME.Equals(o["Name"]) && PRINTER_NAME.Equals(o["DeviceID"]));
            if (_printer == null)
            {
                var _port = Query("Win32_TCPIPPrinterPort").FirstOrDefault(o => PORT_NAME.Equals(o["Name"]));
                if (_port == null)
                {
                    Create("Win32_TCPIPPrinterPort",
                        "Name", PORT_NAME,
                        "Protocol", 1,
                        "HostAddress", "127.0.0.1",
                        "PortNumber", 9100,
                        "SNMPEnabled", false);
                }
                Create("Win32_Printer",
                    "Name", PRINTER_NAME,
                    "DeviceID", PRINTER_NAME,
                    "DriverName", "Generic / Text Only",
                    "PortName", PORT_NAME,
                    "Network", true,
                    "Shared", false);
            }
        }
    }
}
