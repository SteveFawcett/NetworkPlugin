# Network Plugin

## Purpose
Network Plugin is designed to manage and optimize network configurations for various applications. 
It provides tools to monitor network performance, configure settings, and ensure secure connections.

## Configuration [Network]
Using the Stanza "Network" in the configuration file, you can set various parameters to customize the behavior of the Network Plugin.

### Parameters

- `enable`: (boolean) Enable or disable the Network Plugin. Default is `true`.
- `NetworkRefresh`: (integer) Set the interval (in milliseconds) for listing the servers on the network. Default is `60000`.
- `NetworkUpdate`: (integer) Set the interval (in milliseconds) for getting the SNMP details. Default is `1000`.
- `NetworkSubnet`: (string) Specify the subnet to be used for network configurations. Default is `192.168.1`