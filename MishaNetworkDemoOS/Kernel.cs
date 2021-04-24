﻿using Cosmos.HAL;
using Cosmos.System.Network;
using Cosmos.System.Network.Config;
using Cosmos.System.Network.IPv4;
using Cosmos.System.Network.IPv4.UDP;
using Cosmos.System.Network.IPv4.UDP.DHCP;
using Cosmos.System.Network.IPv4.UDP.DNS;
using System;
using System.Text;
using Sys = Cosmos.System;

namespace CosmosNetwork
{
    public class Kernel : Sys.Kernel
    {
        private DnsClient xClient = new DnsClient();
        protected override void BeforeRun()
        {
            try
            {

                using (var xClient = new DHCPClient())
                {
                    /** Send a DHCP Discover packet **/
                    //This will automatically set the IP config after DHCP response
                    NetworkStack.Update();
                    int r = xClient.SendDiscoverPacket();
                    if (r == -1)
                    {
                        Console.WriteLine("Failure while configuring DHCP: timeout");
                        ManualConfig();
                    }
                    else
                    {
                        Console.WriteLine("DHCP Configure: result: " + r);
                    }
                    xClient.Close();  //don't forget to close!
                }
                ipconfig();
            }
            catch (Exception x)
            {
                Console.WriteLine("err: " + x.Message);
            }
        }
        public void ManualConfig()
        {
            Console.WriteLine("Preforming network Manual configuration");
            NetworkDevice nic = NetworkDevice.GetDeviceByName("eth0"); //get network device by name
            IPConfig.Enable(nic,
                new Address(192, 168, 1, 70), //IP
                new Address(255, 255, 255, 252), //SUBNET
                new Address(192, 168, 2, 1) //GATEWAY
                );                      //enable IPv4 configuration

            Console.WriteLine("Manual config complete");
        }
        public void ipconfig()
        {
            if (NetworkStack.ConfigEmpty())
            {
                Console.WriteLine("No network configuration detected!");
            }
            foreach (NetworkDevice device in NetworkConfig.Keys)
            {
                switch (device.CardType)
                {
                    case CardType.Ethernet:
                        Console.Write("Ethernet Card : " + device.NameID + " - " + device.Name);
                        break;
                    case CardType.Wireless:
                        Console.Write("Wireless Card : " + device.NameID + " - " + device.Name);
                        break;
                }
                if (NetworkConfig.CurrentConfig.Key == device)
                {
                    Console.WriteLine(" (current)");
                }
                else
                {
                    Console.WriteLine();
                }

                Console.WriteLine("MAC Address          : " + device.MACAddress.ToString());
                Console.WriteLine("IP Address           : " + NetworkConfig.Get(device).IPAddress.ToString());
                Console.WriteLine("Subnet mask          : " + NetworkConfig.Get(device).SubnetMask.ToString());
                Console.WriteLine("Default Gateway      : " + NetworkConfig.Get(device).DefaultGateway.ToString());
                Console.WriteLine("DNS Nameservers      : ");
                foreach (Address dnsnameserver in DNSConfig.DNSNameservers)
                {
                    Console.WriteLine("                       " + dnsnameserver.ToString());
                }
            }
        }
        protected override void Run()
        {
            NetworkStack.Update();
            Console.Write("> ");
            var input = Console.ReadLine();

            if (input.ToLower().StartsWith("sendpkt"))
            {
                var x = new UdpClient(128);
                x.Connect(Address.Zero, 128);
                x.Send(Encoding.ASCII.GetBytes("Hello from cosmos!"));
                x.Close();
            }
            else if (input.ToLower().StartsWith("getip"))
            {
                Console.WriteLine("Connecting to DNS Server");
                xClient.Connect(new Address(8, 8, 4, 4));

                Console.WriteLine("Asking IP for github.com");
                xClient.SendAsk("github.com");

                Console.WriteLine("Waiting for data");

                var addr = xClient.Receive();
                if (addr == null)
                {
                    Console.WriteLine("Error: connection timed out");
                }
                else
                {
                    Console.WriteLine("Got data: " + addr.ToString());
                }
            }
            else if (input.ToLower().StartsWith("gettime"))
            {
                NTPClient client = new NTPClient();
                var t = client.GetNetworkTime();
                if (t == null)
                {
                    Console.WriteLine("NTPClient.GetNetworkTime() Returned null!");
                }
                else
                {
                    Console.WriteLine("NTPClient.GetNetworkTime() Returned " + t);
                }
            }
            else if (input.ToLower().StartsWith("ping"))
            {
                string s = input.Replace("ping ", "");

                if (input.ToLower() == "ping" | string.IsNullOrEmpty(s))
                {
                    Console.WriteLine("Invaild synax. Usage: ping <ip address or site>");
                    return;
                }

                Address dest = Address.Parse(s);

                if (dest == null)
                {
                    //make a DNS request
                    xClient.Connect(DNSConfig.Server(0));
                    xClient.SendAsk(s);
                    dest = xClient.Receive();
                    xClient.Close();

                    if (dest == null)
                    {
                        Console.WriteLine("ERROR: Cannot find " + s);
                        return;
                    }
                }
                int PacketSent = 0;
                int PacketReceived = 0;
                int PacketLost = 0;
                int PercentLoss;
                try
                {
                    Console.WriteLine("Sending ping to " + dest.ToString());

                    var xClient = new ICMPClient();
                    xClient.Connect(dest);

                    for (int i = 0; i < 4; i++)
                    {
                        xClient.SendEcho();

                        PacketSent++;

                        var endpoint = new EndPoint(Address.Zero, 0);

                        int second = xClient.Receive(ref endpoint, 4000);

                        if (second == -1)
                        {
                            Console.WriteLine("Destination host unreachable.");
                            PacketLost++;
                        }
                        else
                        {
                            if (second < 1)
                            {
                                Console.WriteLine("Reply received from " + endpoint.address.ToString() + " time < 1s");
                            }
                            else if (second >= 1)
                            {
                                Console.WriteLine("Reply received from " + endpoint.address.ToString() + " time " + second + "s");
                            }

                            PacketReceived++;
                        }
                    }

                    xClient.Close();
                }
                catch (Exception x)
                {
                    Console.WriteLine("Ping error: " + x.Message);
                }

                PercentLoss = 25 * PacketLost;

                Console.WriteLine();
                Console.WriteLine("Ping statistics for " + dest.ToString() + ":");
                Console.WriteLine("    Packets: Sent = " + PacketSent + ", Received = " + PacketReceived + ", Lost = " + PacketLost + " (" + PercentLoss + "% loss)");
            }
            else if (input.ToLower().StartsWith("ipconfig"))
            {
                ipconfig();
            }
            else if (input.ToLower().StartsWith("help"))
            {
                Console.WriteLine("MishaNetworkDemoOS Commands");
                Console.WriteLine("ipconfig - Gets current IP config");
                Console.WriteLine("getip - Gets IP address of github.com");
                Console.WriteLine("sendpkt - Sends a packet");
                Console.WriteLine("gettime - Get UTC time from time.windows.com");
            }
            else if (string.IsNullOrEmpty(input)) { }
            else
            {
                Console.WriteLine("Unknown command.");
            }
            NetworkStack.Update();
        }
    }
}
