using System;
using System.Net;

namespace ImmoralityGaming.Extensions
{
    public static class IpAddressExtensions
    {
        public static IPAddress GetBroadcastAddress(this IPAddress address, IPAddress subnetMask)
        {
            var ipAdressBytes = address.GetAddressBytes();
            var subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
            {
                return null;
            }

            var broadcastAddress = new byte[ipAdressBytes.Length];
            for (var i = 0; i <= broadcastAddress.Length - 1; i++)
            {
                broadcastAddress[i] = Convert.ToByte(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }

            return new IPAddress(broadcastAddress);
        }

        public static IPAddress GetNetworkAddress(this IPAddress address, IPAddress subnetMask)
        {
            var ipAdressBytes = address.GetAddressBytes();
            var subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
            {
                return null;
            }

            var broadcastAddress = new byte[ipAdressBytes.Length];
            for (var i = 0; i <= broadcastAddress.Length - 1; i++)
            {
                broadcastAddress[i] = Convert.ToByte(ipAdressBytes[i] & subnetMaskBytes[i]);
            }

            return new IPAddress(broadcastAddress);
        }

        public static bool IsInSameSubnet(this IPAddress address2, IPAddress address, IPAddress subnetMask)
        {
            var network1 = address.GetNetworkAddress(subnetMask);

            if (network1 == null)
            {
                return false;
            }

            var network2 = address2.GetNetworkAddress(subnetMask);

            return network2 != null && network1.Equals(network2);
        }
    }
}